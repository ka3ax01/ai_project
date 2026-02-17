using BookingPlatform.Application.Common;
using BookingPlatform.Application.Notifications;
using BookingPlatform.Application.Notifications.Queries;
using BookingPlatform.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Infrastructure.Notifications;

public sealed class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    private readonly AppDbContext _dbContext;
    private readonly IRequestContextAccessor _requestContextAccessor;

    public GetMyNotificationsQueryHandler(AppDbContext dbContext, IRequestContextAccessor requestContextAccessor)
    {
        _dbContext = dbContext;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task<IReadOnlyList<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = _requestContextAccessor.UserId
                     ?? throw new InvalidOperationException("Current user is not resolved.");

        return await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Type = n.Type,
                PayloadJson = n.PayloadJson,
                Status = n.Status.ToString(),
                CreatedAtUtc = n.CreatedAtUtc,
                SentAtUtc = n.SentAtUtc,
                FailReason = n.FailReason
            })
            .ToListAsync(cancellationToken);
    }
}
