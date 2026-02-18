using MediatR;

namespace BookingPlatform.Application.Bookings.Queries;

public sealed record GetBookingRiskQuery(Guid BookingId) : IRequest<BookingRiskDto>;
