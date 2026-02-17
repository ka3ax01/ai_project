using BookingPlatform.Application.Bookings;
using MediatR;

namespace BookingPlatform.Application.Bookings.Commands;

public sealed record ConfirmBookingCommand(Guid Id) : IRequest<BookingDto>;
