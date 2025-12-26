using MediatR;

namespace BookingPlatform.Application.Bookings.Commands;

public sealed record DeleteBookingCommand(Guid Id) : IRequest;
