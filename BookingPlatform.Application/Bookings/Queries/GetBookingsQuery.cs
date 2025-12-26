using BookingPlatform.Application.Bookings;
using MediatR;

namespace BookingPlatform.Application.Bookings.Queries;

public sealed record GetBookingsQuery : IRequest<IReadOnlyList<BookingDto>>;

