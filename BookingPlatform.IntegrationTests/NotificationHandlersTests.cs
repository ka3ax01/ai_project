using BookingPlatform.Application.Common;
using BookingPlatform.Application.Notifications.Commands;
using BookingPlatform.Application.Notifications.Queries;
using BookingPlatform.Domain.Enums;
using BookingPlatform.Domain.Notifications;
using BookingPlatform.Infrastructure.Notifications;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace BookingPlatform.IntegrationTests;

[Collection("NotificationHandlersTests")]
public sealed class NotificationHandlersTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("booking_platform_notifications_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task UnreadCount_ReturnsPendingOnlyForCurrentUser()
    {
        await ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await SeedNotificationsAsync(userId, otherUserId);

        await using var db = CreateDbContext();
        var handler = new GetUnreadCountQueryHandler(db, new FakeRequestContextAccessor(userId));

        var count = await handler.Handle(new GetUnreadCountQuery(), CancellationToken.None);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetMyNotifications_UnreadFilter_ReturnsOnlyPending()
    {
        await ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await SeedNotificationsAsync(userId, otherUserId);

        await using var db = CreateDbContext();
        var handler = new GetMyNotificationsQueryHandler(db, new FakeRequestContextAccessor(userId));

        var page = await handler.Handle(new GetMyNotificationsQuery("unread", 20, 0), CancellationToken.None);

        Assert.Equal(2, page.Total);
        Assert.All(page.Items, x => Assert.Equal("Pending", x.Status));
    }

    [Fact]
    public async Task MarkRead_UpdatesOnlyCurrentUserPendingRows()
    {
        await ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var seeded = await SeedNotificationsAsync(userId, otherUserId);
        var ids = new[] { seeded.UserPending1, seeded.UserRead, seeded.OtherPending };

        await using var db = CreateDbContext();
        var handler = new MarkNotificationsReadCommandHandler(db, new FakeRequestContextAccessor(userId));

        var updated = await handler.Handle(new MarkNotificationsReadCommand(ids), CancellationToken.None);
        Assert.Equal(1, updated);

        var userPending1 = await db.Notifications.SingleAsync(n => n.Id == seeded.UserPending1);
        var userRead = await db.Notifications.SingleAsync(n => n.Id == seeded.UserRead);
        var otherPending = await db.Notifications.SingleAsync(n => n.Id == seeded.OtherPending);
        Assert.Equal(NotificationStatus.Read, userPending1.Status);
        Assert.Equal(NotificationStatus.Read, userRead.Status);
        Assert.Equal(NotificationStatus.Pending, otherPending.Status);
    }

    [Fact]
    public async Task MarkAllRead_UpdatesRemainingPendingForCurrentUser()
    {
        await ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var seeded = await SeedNotificationsAsync(userId, otherUserId);

        await using var db = CreateDbContext();
        var handler = new MarkAllNotificationsReadCommandHandler(db, new FakeRequestContextAccessor(userId));

        var updated = await handler.Handle(new MarkAllNotificationsReadCommand(), CancellationToken.None);
        Assert.Equal(2, updated);

        var myRows = await db.Notifications.Where(n => n.UserId == userId).ToListAsync();
        Assert.All(myRows, x => Assert.Equal(NotificationStatus.Read, x.Status));

        var otherRow = await db.Notifications.SingleAsync(n => n.Id == seeded.OtherPending);
        Assert.Equal(NotificationStatus.Pending, otherRow.Status);
    }

    private async Task<SeededNotificationIds> SeedNotificationsAsync(Guid userId, Guid otherUserId)
    {
        await using var db = CreateDbContext();

        var userPending1 = Guid.NewGuid();
        var userPending2 = Guid.NewGuid();
        var userRead = Guid.NewGuid();
        var otherPending = Guid.NewGuid();

        db.Notifications.AddRange(
            CreateNotification(userPending1, userId, NotificationStatus.Pending, "BookingCreated"),
            CreateNotification(userPending2, userId, NotificationStatus.Pending, "BookingCancelled"),
            CreateNotification(userRead, userId, NotificationStatus.Read, "BookingConfirmed"),
            CreateNotification(otherPending, otherUserId, NotificationStatus.Pending, "BookingNoShow"));

        await db.SaveChangesAsync();

        return new SeededNotificationIds
        {
            UserPending1 = userPending1,
            UserRead = userRead,
            OtherPending = otherPending
        };
    }

    private static Notification CreateNotification(Guid id, Guid userId, NotificationStatus status, string type) => new()
    {
        Id = id,
        UserId = userId,
        Type = type,
        PayloadJson = "{}",
        Status = status,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    private async Task ResetDatabaseAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeRequestContextAccessor : IRequestContextAccessor
    {
        public FakeRequestContextAccessor(Guid userId)
        {
            UserId = userId;
        }

        public Guid? UserId { get; }
        public string CorrelationId => "test";
        public string TraceId => "test";
        public string? IpAddress => null;
        public string? UserAgent => null;
    }

    private sealed class SeededNotificationIds
    {
        public Guid UserPending1 { get; init; }
        public Guid UserRead { get; init; }
        public Guid OtherPending { get; init; }
    }
}

[CollectionDefinition("NotificationHandlersTests", DisableParallelization = true)]
public sealed class NotificationHandlersTestsCollection;
