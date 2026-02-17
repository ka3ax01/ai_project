using System.Text.Json;
using BookingPlatform.Application.Bookings;
using BookingPlatform.Application.Bookings.Commands;
using BookingPlatform.Application.Bookings.Queries;
using BookingPlatform.Application.Common;
using BookingPlatform.Application.Common.Exceptions;
using BookingPlatform.Domain.Bookings;
using BookingPlatform.Domain.Enums;
using BookingPlatform.Domain.Notifications;
using BookingPlatform.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BookingPlatform.Infrastructure.Bookings;

public sealed class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, BookingDto>
{
    private readonly AppDbContext _dbContext;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly BookingLifecycleOptions _options;

    public CreateBookingCommandHandler(
        AppDbContext dbContext,
        IRequestContextAccessor requestContextAccessor,
        IOptions<BookingLifecycleOptions> options)
    {
        _dbContext = dbContext;
        _requestContextAccessor = requestContextAccessor;
        _options = options.Value;
    }

    public async Task<BookingDto> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _requestContextAccessor.UserId
                            ?? throw new InvalidOperationException("Current user is not resolved.");

        var startUtc = request.StartTimeUtc.ToUniversalTime();
        var endUtc = request.EndTimeUtc.ToUniversalTime();

        if (startUtc >= endUtc)
        {
            throw new InvalidOperationException("Start time must be less than end time.");
        }

        // IMPORTANT: EF Core cannot translate custom C# methods inside LINQ-to-SQL.
        // Use Contains(...) so it becomes SQL IN (...)
        var blockingStatuses = new[]
        {
            BookingStatus.Pending,
            BookingStatus.Confirmed,
            BookingStatus.InProgress
        };

        var hasOverlap = await _dbContext.Bookings
            .AsNoTracking()
            .AnyAsync(
                b => b.RoomId == request.RoomId
                     && blockingStatuses.Contains(b.Status)
                     && b.StartTimeUtc < endUtc
                     && b.EndTimeUtc > startUtc,
                cancellationToken);

        if (hasOverlap)
        {
            throw new BookingConflictException("The resource is already booked for the requested time range.");
        }

        var confirmationWindow = _options.ConfirmationWindowMinutes <= 0 ? 15 : _options.ConfirmationWindowMinutes;

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            RoomId = request.RoomId,
            UserId = currentUserId,
            StartTimeUtc = startUtc,
            EndTimeUtc = endUtc,
            ConfirmByUtc = startUtc.AddMinutes(-confirmationWindow),
            ConfirmedAtUtc = null,
            Status = BookingStatus.Pending,
            Purpose = request.Purpose,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = currentUserId,
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.Bookings.Add(booking);

            _dbContext.BookingAuditLogs.Add(BookingAuditHelpers.CreateAudit(
                booking.Id,
                "BookingCreated",
                null,
                booking.Status,
                _requestContextAccessor));

            _dbContext.Notifications.Add(NotificationFactory.Create(
                booking.UserId,
                "BookingCreated",
                new { booking.Id, booking.RoomId, booking.StartTimeUtc, booking.EndTimeUtc, booking.Status }));

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (BookingAuditHelpers.IsExclusionConflict(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new BookingConflictException("The resource is already booked for the requested time range.");
        }

        return BookingMappings.ToDto(booking);
    }
}

public sealed class ConfirmBookingCommandHandler : IRequestHandler<ConfirmBookingCommand, BookingDto>
{
    private readonly AppDbContext _dbContext;
    private readonly IRequestContextAccessor _requestContextAccessor;

    public ConfirmBookingCommandHandler(AppDbContext dbContext, IRequestContextAccessor requestContextAccessor)
    {
        _dbContext = dbContext;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task<BookingDto> Handle(ConfirmBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await _dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (booking is null)
        {
            throw new KeyNotFoundException("Booking not found");
        }

        if (booking.Status != BookingStatus.Pending)
        {
            throw new BookingNotConfirmableException("Booking is not in pending status.");
        }

        var now = DateTimeOffset.UtcNow;
        if (booking.ConfirmByUtc.HasValue && now > booking.ConfirmByUtc.Value)
        {
            throw new BookingConfirmationExpiredException("Booking confirmation deadline has expired.");
        }

        var oldStatus = booking.Status;
        booking.Status = BookingStatus.Confirmed;
        booking.ConfirmedAtUtc = now;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.BookingAuditLogs.Add(BookingAuditHelpers.CreateAudit(
                booking.Id,
                "BookingStatusChanged",
                oldStatus,
                booking.Status,
                _requestContextAccessor));

            _dbContext.BookingAuditLogs.Add(BookingAuditHelpers.CreateAudit(
                booking.Id,
                "BookingConfirmed",
                oldStatus,
                booking.Status,
                _requestContextAccessor));

            _dbContext.Notifications.Add(NotificationFactory.Create(
                booking.UserId,
                "BookingConfirmed",
                new { booking.Id, booking.RoomId, booking.StartTimeUtc, booking.EndTimeUtc }));

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (BookingAuditHelpers.IsExclusionConflict(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new BookingConflictException("The resource is already booked for the requested time range.");
        }

        return BookingMappings.ToDto(booking);
    }
}

public sealed class UpdateBookingStatusCommandHandler : IRequestHandler<UpdateBookingStatusCommand, BookingDto>
{
    private readonly AppDbContext _dbContext;
    private readonly IRequestContextAccessor _requestContextAccessor;

    public UpdateBookingStatusCommandHandler(AppDbContext dbContext, IRequestContextAccessor requestContextAccessor)
    {
        _dbContext = dbContext;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task<BookingDto> Handle(UpdateBookingStatusCommand request, CancellationToken cancellationToken)
    {
        var booking = await _dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (booking is null)
        {
            throw new KeyNotFoundException("Booking not found");
        }

        if (!Enum.TryParse<BookingStatus>(request.Status, true, out var newStatus))
        {
            throw new InvalidOperationException($"Unsupported booking status '{request.Status}'.");
        }

        var oldStatus = booking.Status;
        BookingStatusRules.EnsureTransitionAllowed(oldStatus, newStatus);

        if (newStatus == BookingStatus.Confirmed)
        {
            booking.ConfirmedAtUtc = DateTimeOffset.UtcNow;
        }

        booking.Status = newStatus;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.BookingAuditLogs.Add(BookingAuditHelpers.CreateAudit(
                booking.Id,
                "BookingStatusChanged",
                oldStatus,
                newStatus,
                _requestContextAccessor));

            if (newStatus == BookingStatus.Cancelled)
            {
                _dbContext.BookingAuditLogs.Add(BookingAuditHelpers.CreateAudit(
                    booking.Id,
                    "BookingCancelled",
                    oldStatus,
                    newStatus,
                    _requestContextAccessor));

                _dbContext.Notifications.Add(NotificationFactory.Create(
                    booking.UserId,
                    "BookingCancelled",
                    new { booking.Id, booking.RoomId, booking.StartTimeUtc, booking.EndTimeUtc }));
            }

            if (newStatus == BookingStatus.NoShow)
            {
                _dbContext.Notifications.Add(NotificationFactory.Create(
                    booking.UserId,
                    "BookingNoShow",
                    new { booking.Id, booking.RoomId, booking.StartTimeUtc, booking.EndTimeUtc }));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (BookingAuditHelpers.IsExclusionConflict(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new BookingConflictException("The resource is already booked for the requested time range.");
        }

        return BookingMappings.ToDto(booking);
    }
}

public sealed class DeleteBookingCommandHandler : IRequestHandler<DeleteBookingCommand>
{
    private readonly AppDbContext _dbContext;
    private readonly IRequestContextAccessor _requestContextAccessor;

    public DeleteBookingCommandHandler(AppDbContext dbContext, IRequestContextAccessor requestContextAccessor)
    {
        _dbContext = dbContext;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task<Unit> Handle(DeleteBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await _dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (booking is null)
        {
            return Unit.Value;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.BookingAuditLogs.Add(BookingAuditHelpers.CreateAudit(
            booking.Id,
            "BookingDeleted",
            booking.Status,
            null,
            _requestContextAccessor));

        _dbContext.Bookings.Remove(booking);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class GetBookingsQueryHandler : IRequestHandler<GetBookingsQuery, IReadOnlyList<BookingDto>>
{
    private readonly AppDbContext _dbContext;

    public GetBookingsQueryHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<BookingDto>> Handle(GetBookingsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Bookings
            .AsNoTracking()
            .Join(_dbContext.Rooms.AsNoTracking(),
                b => b.RoomId,
                r => r.Id,
                (b, r) => new { b, r })
            .Join(_dbContext.Users.AsNoTracking(),
                x => x.b.UserId,
                u => u.Id,
                (x, u) => new { x.b, x.r, u })
            .Select(x => new BookingDto
            {
                Id = x.b.Id,
                RoomId = x.b.RoomId,
                UserId = x.b.UserId,
                StartTimeUtc = x.b.StartTimeUtc.ToLocalTime(),
                EndTimeUtc = x.b.EndTimeUtc.ToLocalTime(),
                ConfirmByUtc = x.b.ConfirmByUtc,
                ConfirmedAtUtc = x.b.ConfirmedAtUtc,
                Status = x.b.Status.ToString(),
                Purpose = x.b.Purpose,
                RoomName = x.r.Number,
                Username = x.u.Username
            })
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetBookingByIdQueryHandler : IRequestHandler<GetBookingByIdQuery, BookingDto?>
{
    private readonly AppDbContext _dbContext;

    public GetBookingByIdQueryHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BookingDto?> Handle(GetBookingByIdQuery request, CancellationToken cancellationToken)
    {
        var result = await _dbContext.Bookings
            .AsNoTracking()
            .Where(b => b.Id == request.Id)
            .Join(_dbContext.Rooms.AsNoTracking(),
                b => b.RoomId,
                r => r.Id,
                (b, r) => new { b, r })
            .Join(_dbContext.Users.AsNoTracking(),
                x => x.b.UserId,
                u => u.Id,
                (x, u) => new BookingDto
                {
                    Id = x.b.Id,
                    RoomId = x.b.RoomId,
                    UserId = x.b.UserId,
                    StartTimeUtc = x.b.StartTimeUtc,
                    EndTimeUtc = x.b.EndTimeUtc,
                    ConfirmByUtc = x.b.ConfirmByUtc,
                    ConfirmedAtUtc = x.b.ConfirmedAtUtc,
                    Status = x.b.Status.ToString(),
                    Purpose = x.b.Purpose,
                    RoomName = x.r.Number,
                    Username = u.Username
                })
            .FirstOrDefaultAsync(cancellationToken);

        return result;
    }
}

internal static class BookingMappings
{
    public static BookingDto ToDto(Booking booking) => new()
    {
        Id = booking.Id,
        RoomId = booking.RoomId,
        UserId = booking.UserId,
        StartTimeUtc = booking.StartTimeUtc,
        EndTimeUtc = booking.EndTimeUtc,
        ConfirmByUtc = booking.ConfirmByUtc,
        ConfirmedAtUtc = booking.ConfirmedAtUtc,
        Status = booking.Status.ToString(),
        Purpose = booking.Purpose
    };
}

internal static class BookingStatusRules
{
    // Keep this for domain logic / non-LINQ usage (e.g., transitions, background jobs).
    // Do not call this inside EF LINQ queries.
    public static bool BlocksOverlap(BookingStatus status) =>
        status is BookingStatus.Pending or BookingStatus.Confirmed or BookingStatus.InProgress;

    public static void EnsureTransitionAllowed(BookingStatus current, BookingStatus next)
    {
        if (current == next)
        {
            return;
        }

        var allowed = current switch
        {
            BookingStatus.Pending => next is BookingStatus.Confirmed or BookingStatus.Cancelled,
            BookingStatus.Confirmed => next is BookingStatus.InProgress or BookingStatus.Cancelled or BookingStatus.NoShow,
            BookingStatus.InProgress => next is BookingStatus.Completed or BookingStatus.NoShow,
            BookingStatus.Completed => false,
            BookingStatus.Cancelled => false,
            BookingStatus.NoShow => false,
            _ => false
        };

        if (!allowed)
        {
            throw new BookingInvalidStatusTransitionException($"Invalid booking status transition: {current} -> {next}.");
        }
    }
}

internal static class BookingAuditHelpers
{
    public static bool IsExclusionConflict(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation };

    public static BookingAuditLog CreateAudit(
        Guid bookingId,
        string action,
        BookingStatus? oldStatus,
        BookingStatus? newStatus,
        IRequestContextAccessor requestContextAccessor) => new()
    {
        Id = Guid.NewGuid(),
        BookingId = bookingId,
        Action = action,
        OldStatus = oldStatus is null ? null : (int)oldStatus.Value,
        NewStatus = newStatus is null ? null : (int)newStatus.Value,
        ActorUserId = requestContextAccessor.UserId,
        OccurredAtUtc = DateTimeOffset.UtcNow,
        CorrelationId = string.IsNullOrWhiteSpace(requestContextAccessor.CorrelationId)
            ? requestContextAccessor.TraceId
            : requestContextAccessor.CorrelationId,
        IpAddress = requestContextAccessor.IpAddress,
        UserAgent = requestContextAccessor.UserAgent
    };

    public static BookingAuditLog CreateSystemAudit(
        Guid bookingId,
        BookingStatus oldStatus,
        BookingStatus newStatus) => new()
    {
        Id = Guid.NewGuid(),
        BookingId = bookingId,
        Action = "SystemJob",
        OldStatus = (int)oldStatus,
        NewStatus = (int)newStatus,
        ActorUserId = null,
        OccurredAtUtc = DateTimeOffset.UtcNow,
        CorrelationId = "system"
    };
}

internal static class NotificationFactory
{
    public static Notification Create(Guid userId, string type, object payload) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Type = type,
        PayloadJson = JsonSerializer.Serialize(payload),
        Status = NotificationStatus.Pending,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };
}
