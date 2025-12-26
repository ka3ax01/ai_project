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
    private readonly AppDbContext _db;

    public CreateBookingCommandHandler(AppDbContext db) => _db = db;

    public async Task<BookingDto> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = new Booking
        {
            RoomId = request.RoomId,
            UserId = request.UserId,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc,
            Status = BookingStatus.Pending,
            Purpose = request.Purpose,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(cancellationToken);

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
    private readonly AppDbContext _db;

    public UpdateBookingStatusCommandHandler(AppDbContext db) => _db = db;

    public async Task<BookingDto> Handle(UpdateBookingStatusCommand request, CancellationToken cancellationToken)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (booking is null)
        {
            throw new KeyNotFoundException("Booking not found");
        }

        booking.Status = Enum.Parse<BookingStatus>(request.Status, ignoreCase: true);
        await _db.SaveChangesAsync(cancellationToken);

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
    private readonly AppDbContext _db;

    public DeleteBookingCommandHandler(AppDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (booking is null)
        {
            return Unit.Value;
        }

        _db.Bookings.Remove(booking);
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class GetBookingByIdQueryHandler : IRequestHandler<GetBookingByIdQuery, BookingDto?>
{
    private readonly AppDbContext _db;

    public GetBookingByIdQueryHandler(AppDbContext db) => _db = db;

    public async Task<BookingDto?> Handle(GetBookingByIdQuery request, CancellationToken cancellationToken)
    {
        var booking = await _db.Bookings.AsNoTracking()
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

public sealed class GetBookingsQueryHandler : IRequestHandler<GetBookingsQuery, IReadOnlyList<BookingDto>>
{
    private readonly AppDbContext _db;

    public GetBookingsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<BookingDto>> Handle(GetBookingsQuery request, CancellationToken cancellationToken)
    {
        return await _db.Bookings.AsNoTracking()
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
