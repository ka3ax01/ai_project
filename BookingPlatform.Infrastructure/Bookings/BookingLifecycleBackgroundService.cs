using BookingPlatform.Domain.Enums;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookingPlatform.Infrastructure.Bookings;

public sealed class BookingLifecycleBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingLifecycleBackgroundService> _logger;
    private readonly BookingLifecycleOptions _options;

    public BookingLifecycleBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingLifecycleBackgroundService> logger,
        IOptions<BookingLifecycleOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Booking lifecycle job failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        await AutoCancelExpiredPendingAsync(dbContext, now, cancellationToken);
        await DetectNoShowsAsync(dbContext, now, cancellationToken);
    }

    private static async Task AutoCancelExpiredPendingAsync(
        AppDbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var expired = await dbContext.Bookings
            .Where(b => b.Status == BookingStatus.Pending
                        && b.ConfirmByUtc.HasValue
                        && b.ConfirmByUtc.Value < now)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var booking in expired)
        {
            var oldStatus = booking.Status;
            booking.Status = BookingStatus.Cancelled;

            dbContext.BookingAuditLogs.Add(BookingAuditHelpers.CreateSystemAudit(
                booking.Id,
                oldStatus,
                BookingStatus.Cancelled));
            dbContext.Notifications.Add(NotificationFactory.Create(
                booking.UserId,
                "BookingAutoCancelled",
                new { booking.Id, booking.RoomId, Reason = "confirmation expired" }));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private async Task DetectNoShowsAsync(
        AppDbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var grace = _options.NoShowGraceMinutes <= 0 ? 10 : _options.NoShowGraceMinutes;
        var threshold = now.AddMinutes(-grace);

        var toNoShow = await dbContext.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed
                        && b.StartTimeUtc < threshold)
            .ToListAsync(cancellationToken);

        if (toNoShow.Count == 0)
        {
            return;
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var booking in toNoShow)
        {
            var oldStatus = booking.Status;
            booking.Status = BookingStatus.NoShow;

            dbContext.BookingAuditLogs.Add(BookingAuditHelpers.CreateSystemAudit(
                booking.Id,
                oldStatus,
                BookingStatus.NoShow));
            dbContext.Notifications.Add(NotificationFactory.Create(
                booking.UserId,
                "BookingNoShow",
                new { booking.Id, booking.RoomId, booking.StartTimeUtc }));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }
}
