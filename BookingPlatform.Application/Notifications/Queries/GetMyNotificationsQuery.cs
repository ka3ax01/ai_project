using MediatR;

namespace BookingPlatform.Application.Notifications.Queries;

public sealed record GetMyNotificationsQuery : IRequest<IReadOnlyList<NotificationDto>>;
