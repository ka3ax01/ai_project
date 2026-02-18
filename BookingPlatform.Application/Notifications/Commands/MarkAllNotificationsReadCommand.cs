using MediatR;

namespace BookingPlatform.Application.Notifications.Commands;

public sealed record MarkAllNotificationsReadCommand : IRequest<int>;
