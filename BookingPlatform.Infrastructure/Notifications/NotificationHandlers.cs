using BookingPlatform.Application.Common;
using BookingPlatform.Application.Notifications;
using BookingPlatform.Application.Notifications.Commands;
using BookingPlatform.Application.Notifications.Queries;
using BookingPlatform.Domain.Enums;
using BookingPlatform.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Infrastructure.Notifications;

public sealed class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, PagedResult<NotificationDto>>
{
    private readonly AppDbContext _dbContext;
    private readonly IRequestContextAccessor _requestContextAccessor;

    public GetMyNotificationsQueryHandler(AppDbContext dbContext, IRequestContextAccessor requestContextAccessor)
    {
        _dbContext = dbContext;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task<PagedResult<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = _requestContextAccessor.UserId
                     ?? throw new InvalidOperationException("Current user is not resolved.");

        var unreadOnly = string.Equals(request.StatusFilter, "unread", StringComparison.OrdinalIgnoreCase);

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => n.Status == NotificationStatus.Pending);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(n => n.Status == NotificationStatus.Pending)
            .ThenByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.Status,
                n.CreatedAtUtc,
                n.PayloadJson
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Status = n.Status.ToString(),
                CreatedAtUtc = n.CreatedAtUtc,
                Message = NotificationMessageFormatter.Format(n.Type, n.PayloadJson),
                Payload = n.PayloadJson
            })
            .ToList();

        return new PagedResult<NotificationDto>
        {
            Total = total,
            Items = items
        };
    }
}

public sealed class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, int>
{
    private readonly AppDbContext _dbContext;
    private readonly IRequestContextAccessor _requestContextAccessor;

    public GetUnreadCountQueryHandler(AppDbContext dbContext, IRequestContextAccessor requestContextAccessor)
    {
        _dbContext = dbContext;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task<int> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var userId = _requestContextAccessor.UserId
                     ?? throw new InvalidOperationException("Current user is not resolved.");

        return await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(
                n => n.UserId == userId && n.Status == NotificationStatus.Pending,
                cancellationToken);
    }
}

public sealed class MarkNotificationsReadCommandHandler : IRequestHandler<MarkNotificationsReadCommand, int>
{
    private readonly AppDbContext _dbContext;
    private readonly IRequestContextAccessor _requestContextAccessor;

    public MarkNotificationsReadCommandHandler(AppDbContext dbContext, IRequestContextAccessor requestContextAccessor)
    {
        _dbContext = dbContext;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task<int> Handle(MarkNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _requestContextAccessor.UserId
                     ?? throw new InvalidOperationException("Current user is not resolved.");

        var ids = request.Ids
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return 0;
        }

        return await _dbContext.Notifications
            .Where(n => n.UserId == userId
                        && ids.Contains(n.Id)
                        && n.Status == NotificationStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, NotificationStatus.Read),
                cancellationToken);
    }
}

public sealed class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, int>
{
    private readonly AppDbContext _dbContext;
    private readonly IRequestContextAccessor _requestContextAccessor;

    public MarkAllNotificationsReadCommandHandler(AppDbContext dbContext, IRequestContextAccessor requestContextAccessor)
    {
        _dbContext = dbContext;
        _requestContextAccessor = requestContextAccessor;
    }

    public async Task<int> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _requestContextAccessor.UserId
                     ?? throw new InvalidOperationException("Current user is not resolved.");

        return await _dbContext.Notifications
            .Where(n => n.UserId == userId && n.Status == NotificationStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, NotificationStatus.Read),
                cancellationToken);
    }
}
