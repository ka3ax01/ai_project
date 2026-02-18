using BookingPlatform.Domain.Enums;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BookingPlatform.Infrastructure.Bookings;

public sealed class BookingLifecycleProcessor : IBookingLifecycleProcessor
{
    private const int BatchSize = 200;

    private readonly AppDbContext _dbContext;
    private readonly BookingLifecycleJobOptions _options;

    public BookingLifecycleProcessor(AppDbContext dbContext, IOptions<BookingLifecycleJobOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<BookingLifecycleProcessResult> ProcessAsync(CancellationToken cancellationToken)
    {
        var result = new BookingLifecycleProcessResult();
        var now = DateTimeOffset.UtcNow;
        var noShowGraceMinutes = _options.NoShowGraceMinutes <= 0 ? 10 : _options.NoShowGraceMinutes;
        var noShowThreshold = now.AddMinutes(-noShowGraceMinutes);

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        result.ExpiredPendingCount = await ExpirePendingAsync(now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_options.AutoStartEnabled)
        {
            result.AutoStartedCount = await AutoStartAsync(now, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (_options.AutoCompleteEnabled)
        {
            result.AutoCompletedCount = await AutoCompleteAsync(now, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        result.NoShowCount = await MarkNoShowAsync(noShowThreshold, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<int> ExpirePendingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.Bookings
            .Where(b => b.Status == BookingStatus.Pending
                        && b.ConfirmByUtc.HasValue
                        && b.ConfirmByUtc.Value < now)
            .OrderBy(b => b.StartTimeUtc)
            .ThenBy(b => b.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var booking in candidates)
        {
            var oldStatus = booking.Status;
            booking.Status = BookingStatus.Cancelled;

            _dbContext.BookingAuditLogs.Add(BookingAuditHelpers.CreateSystemAudit(
                booking.Id,
                oldStatus,
                BookingStatus.Cancelled));
            _dbContext.Notifications.Add(NotificationFactory.Create(
                booking.UserId,
                "BookingExpired",
                new { booking.Id, booking.RoomId, booking.StartTimeUtc, booking.EndTimeUtc }));
        }

        return candidates.Count;
    }

    private async Task<int> AutoStartAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed
                        && b.StartTimeUtc <= now
                        && now <= b.EndTimeUtc)
            .OrderBy(b => b.StartTimeUtc)
            .ThenBy(b => b.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var booking in candidates)
        {
            var oldStatus = booking.Status;
            booking.Status = BookingStatus.InProgress;

            _dbContext.BookingAuditLogs.Add(BookingAuditHelpers.CreateSystemAudit(
                booking.Id,
                oldStatus,
                BookingStatus.InProgress));
            _dbContext.Notifications.Add(NotificationFactory.Create(
                booking.UserId,
                "BookingStarted",
                new { booking.Id, booking.RoomId, booking.StartTimeUtc, booking.EndTimeUtc }));
        }

        return candidates.Count;
    }

    private async Task<int> AutoCompleteAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.Bookings
            .Where(b => b.Status == BookingStatus.InProgress
                        && b.EndTimeUtc <= now)
            .OrderBy(b => b.EndTimeUtc)
            .ThenBy(b => b.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var booking in candidates)
        {
            var oldStatus = booking.Status;
            booking.Status = BookingStatus.Completed;

            _dbContext.BookingAuditLogs.Add(BookingAuditHelpers.CreateSystemAudit(
                booking.Id,
                oldStatus,
                BookingStatus.Completed));
            _dbContext.Notifications.Add(NotificationFactory.Create(
                booking.UserId,
                "BookingCompleted",
                new { booking.Id, booking.RoomId, booking.StartTimeUtc, booking.EndTimeUtc }));
        }

        return candidates.Count;
    }

    private async Task<int> MarkNoShowAsync(DateTimeOffset noShowThreshold, CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed
                        && b.StartTimeUtc < noShowThreshold)
            .OrderBy(b => b.StartTimeUtc)
            .ThenBy(b => b.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var booking in candidates)
        {
            var oldStatus = booking.Status;
            booking.Status = BookingStatus.NoShow;

            _dbContext.BookingAuditLogs.Add(BookingAuditHelpers.CreateSystemAudit(
                booking.Id,
                oldStatus,
                BookingStatus.NoShow));
            _dbContext.Notifications.Add(NotificationFactory.Create(
                booking.UserId,
                "BookingNoShow",
                new { booking.Id, booking.RoomId, booking.StartTimeUtc, booking.EndTimeUtc }));
        }

        return candidates.Count;
    }
}
