using MediatR;

namespace BookingPlatform.Application.Bookings.Commands;

public sealed record CreateBookingCommand(
    Guid RoomId,
    Guid UserId,
    DateTimeOffset StartTimeUtc,
    DateTimeOffset EndTimeUtc,
    string Purpose
) : IRequest<BookingDto>;
