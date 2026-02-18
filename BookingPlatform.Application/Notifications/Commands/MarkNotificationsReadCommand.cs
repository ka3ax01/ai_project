using MediatR;

namespace BookingPlatform.Application.Notifications.Commands;

public sealed record MarkNotificationsReadCommand(IReadOnlyList<Guid> Ids) : IRequest<int>;
