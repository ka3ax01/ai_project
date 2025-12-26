using BookingPlatform.Application.Bookings;
using MediatR;

namespace BookingPlatform.Application.Bookings.Commands;

public sealed record UpdateBookingStatusCommand(
    Guid Id,
    string Status
) : IRequest<BookingDto>;

