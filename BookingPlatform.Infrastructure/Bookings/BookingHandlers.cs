using BookingPlatform.Application.Bookings;
using BookingPlatform.Application.Bookings.Commands;
using BookingPlatform.Application.Bookings.Queries;
using BookingPlatform.Application.Common;
using BookingPlatform.Application.Common.Exceptions;
using BookingPlatform.Domain.Bookings;
using BookingPlatform.Domain.Enums;
using BookingPlatform.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookingPlatform.Infrastructure.Bookings;

public sealed class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, BookingDto>
{
    private readonly AppDbContext _dbContext;
    private readonly IRequestContextAccessor _requestContextAccessor;

    public CreateBookingCommandHandler(AppDbContext dbContext, IRequestContextAccessor requestContextAccessor)
    {
        _dbContext = dbContext;
        _requestContextAccessor = requestContextAccessor;
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

        var hasOverlap = await _dbContext.Bookings
            .AsNoTracking()
            .AnyAsync(
                b => b.RoomId == request.RoomId
                     && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                     && b.StartTimeUtc < endUtc
                     && b.EndTimeUtc > startUtc,
                cancellationToken);
        if (hasOverlap)
        {
            throw new BookingConflictException("The resource is already booked for the requested time range.");
        }

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            RoomId = request.RoomId,
            UserId = currentUserId,
            StartTimeUtc = startUtc,
            EndTimeUtc = endUtc,
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
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (BookingAuditHelpers.IsExclusionConflict(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new BookingConflictException("The resource is already booked for the requested time range.");
        }

        return ToDto(booking);
    }

    private static BookingDto ToDto(Booking booking) => new()
    {
        Id = booking.Id,
        RoomId = booking.RoomId,
        UserId = booking.UserId,
        StartTimeUtc = booking.StartTimeUtc,
        EndTimeUtc = booking.EndTimeUtc,
        Status = booking.Status.ToString(),
        Purpose = booking.Purpose
    };
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
        booking.Status = newStatus;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.BookingAuditLogs.Add(BookingAuditHelpers.CreateAudit(
                booking.Id,
                newStatus == BookingStatus.Cancelled ? "BookingCancelled" : "BookingStatusChanged",
                oldStatus,
                newStatus,
                _requestContextAccessor));
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (BookingAuditHelpers.IsExclusionConflict(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new BookingConflictException("The resource is already booked for the requested time range.");
        }

        return new BookingDto
        {
            Id = booking.Id,
            RoomId = booking.RoomId,
            UserId = booking.UserId,
            StartTimeUtc = booking.StartTimeUtc,
            EndTimeUtc = booking.EndTimeUtc,
            Status = booking.Status.ToString(),
            Purpose = booking.Purpose
        };
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
                Status = x.b.Status.ToString(),
                Purpose = x.b.Purpose,
                RoomName = x.r.Number,
                Username = x.u.Username
            })
            .ToListAsync(cancellationToken);
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
                    Status = x.b.Status.ToString(),
                    Purpose = x.b.Purpose,
                    RoomName = x.r.Number,
                    Username = u.Username
                })
            .FirstOrDefaultAsync(cancellationToken);

        return result;
    }
}
