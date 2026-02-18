using MediatR;
using BookingPlatform.Application.Common;

namespace BookingPlatform.Application.Notifications.Queries;

public sealed record GetMyNotificationsQuery(
    string StatusFilter = "all",
    int Take = 20,
    int Skip = 0) : IRequest<PagedResult<NotificationDto>>;
