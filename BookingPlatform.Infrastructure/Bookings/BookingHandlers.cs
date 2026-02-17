using BookingPlatform.Application.Bookings;
using BookingPlatform.Application.Bookings.Commands;
using BookingPlatform.Application.Bookings.Queries;
using BookingPlatform.Application.Common;
using BookingPlatform.Domain.Bookings;
using BookingPlatform.Domain.Enums;
using BookingPlatform.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Infrastructure.Bookings;

public sealed class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, BookingDto>
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateBookingCommandHandler(AppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<BookingDto> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId
                            ?? throw new InvalidOperationException("Current user is not resolved.");

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            RoomId = request.RoomId,
            UserId = currentUserId,
            StartTimeUtc = request.StartTimeUtc.ToUniversalTime(),
            EndTimeUtc = request.EndTimeUtc.ToUniversalTime(),
            Status = BookingStatus.Pending,
            Purpose = request.Purpose,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = currentUserId,
        };

        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

    public UpdateBookingStatusCommandHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BookingDto> Handle(UpdateBookingStatusCommand request, CancellationToken cancellationToken)
    {
        var booking = await _dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (booking is null)
        {
            throw new KeyNotFoundException("Booking not found");
        }

        booking.Status = Enum.Parse<BookingStatus>(request.Status, ignoreCase: true);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

    public DeleteBookingCommandHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Unit> Handle(DeleteBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await _dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (booking is null)
        {
            return Unit.Value;
        }

        _dbContext.Bookings.Remove(booking);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
