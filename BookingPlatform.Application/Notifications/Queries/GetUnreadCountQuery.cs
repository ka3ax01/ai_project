using MediatR;

namespace BookingPlatform.Application.Notifications.Queries;

public sealed record GetUnreadCountQuery : IRequest<int>;
