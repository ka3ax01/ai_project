using BookingPlatform.Application.Bookings;
using BookingPlatform.Application.Bookings.Commands;
using BookingPlatform.Application.Bookings.Queries;
using BookingPlatform.Domain.Bookings;
using BookingPlatform.Domain.Enums;
using BookingPlatform.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Infrastructure.Bookings;

public sealed class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, BookingDto>
{
    private readonly AppDbContext _dbContext;

    public CreateBookingCommandHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BookingDto> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            RoomId = request.RoomId,
            UserId = request.UserId,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc,
            Status = BookingStatus.Pending,
            Purpose = request.Purpose,
            CreatedAtUtc = DateTimeOffset.UtcNow
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
        return await _dbContext.Bookings.AsNoTracking()
            .Select(b => new BookingDto
            {
                Id = b.Id,
                RoomId = b.RoomId,
                UserId = b.UserId,
                StartTimeUtc = b.StartTimeUtc,
                EndTimeUtc = b.EndTimeUtc,
                Status = b.Status.ToString(),
                Purpose = b.Purpose
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
        var booking = await _dbContext.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        return booking is null
            ? null
            : new BookingDto
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

