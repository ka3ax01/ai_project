using MediatR;

namespace BookingPlatform.Application.Bookings.Queries;

public sealed record GetBookingByIdQuery(Guid Id) : IRequest<BookingDto?>;

