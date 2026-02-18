using BookingPlatform.Domain.Bookings;
using BookingPlatform.Domain.Enums;
using BookingPlatform.Infrastructure.Bookings;
using BookingPlatform.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace BookingPlatform.IntegrationTests;

[Collection("BookingLifecycleProcessorTests")]
public sealed class BookingLifecycleProcessorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public BookingLifecycleProcessorTests()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("booking_platform_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
    }

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
    public async Task PendingExpired_BecomesCancelled_WritesAuditAndNotification()
    {
        await ResetDatabaseAsync();

        var bookingId = Guid.NewGuid();
        await SeedBookingAsync(new Booking
        {
            Id = bookingId,
            RoomId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartTimeUtc = DateTimeOffset.UtcNow.AddHours(1),
            EndTimeUtc = DateTimeOffset.UtcNow.AddHours(2),
            ConfirmByUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            Status = BookingStatus.Pending,
            Purpose = "test",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = Guid.NewGuid()
        });

        await RunProcessorAsync();

        await using var db = CreateDbContext();
        var booking = await db.Bookings.SingleAsync(x => x.Id == bookingId);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.True(await db.BookingAuditLogs.AnyAsync(x => x.BookingId == bookingId && x.NewStatus == (int)BookingStatus.Cancelled));
        Assert.True(await db.Notifications.AnyAsync(x => x.UserId == booking.UserId && x.Type == "BookingExpired"));
    }

    [Fact]
    public async Task ConfirmedAtStart_BecomesInProgress_WritesAuditAndNotification()
    {
        await ResetDatabaseAsync();

        var bookingId = Guid.NewGuid();
        await SeedBookingAsync(new Booking
        {
            Id = bookingId,
            RoomId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartTimeUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            EndTimeUtc = DateTimeOffset.UtcNow.AddHours(1),
            Status = BookingStatus.Confirmed,
            Purpose = "test",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = Guid.NewGuid()
        });

        await RunProcessorAsync();

        await using var db = CreateDbContext();
        var booking = await db.Bookings.SingleAsync(x => x.Id == bookingId);
        Assert.Equal(BookingStatus.InProgress, booking.Status);
        Assert.True(await db.BookingAuditLogs.AnyAsync(x => x.BookingId == bookingId && x.NewStatus == (int)BookingStatus.InProgress));
        Assert.True(await db.Notifications.AnyAsync(x => x.UserId == booking.UserId && x.Type == "BookingStarted"));
    }

    [Fact]
    public async Task InProgressEnded_BecomesCompleted_WritesAuditAndNotification()
    {
        await ResetDatabaseAsync();

        var bookingId = Guid.NewGuid();
        await SeedBookingAsync(new Booking
        {
            Id = bookingId,
            RoomId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartTimeUtc = DateTimeOffset.UtcNow.AddHours(-2),
            EndTimeUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            Status = BookingStatus.InProgress,
            Purpose = "test",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = Guid.NewGuid()
        });

        await RunProcessorAsync();

        await using var db = CreateDbContext();
        var booking = await db.Bookings.SingleAsync(x => x.Id == bookingId);
        Assert.Equal(BookingStatus.Completed, booking.Status);
        Assert.True(await db.BookingAuditLogs.AnyAsync(x => x.BookingId == bookingId && x.NewStatus == (int)BookingStatus.Completed));
        Assert.True(await db.Notifications.AnyAsync(x => x.UserId == booking.UserId && x.Type == "BookingCompleted"));
    }

    [Fact]
    public async Task ConfirmedPastGrace_BecomesNoShow_WritesAuditAndNotification()
    {
        await ResetDatabaseAsync();

        var bookingId = Guid.NewGuid();
        await SeedBookingAsync(new Booking
        {
            Id = bookingId,
            RoomId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartTimeUtc = DateTimeOffset.UtcNow.AddMinutes(-30),
            EndTimeUtc = DateTimeOffset.UtcNow.AddHours(1),
            Status = BookingStatus.Confirmed,
            Purpose = "test",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = Guid.NewGuid()
        });

        await RunProcessorAsync();

        await using var db = CreateDbContext();
        var booking = await db.Bookings.SingleAsync(x => x.Id == bookingId);
        Assert.Equal(BookingStatus.NoShow, booking.Status);
        Assert.True(await db.BookingAuditLogs.AnyAsync(x => x.BookingId == bookingId && x.NewStatus == (int)BookingStatus.NoShow));
        Assert.True(await db.Notifications.AnyAsync(x => x.UserId == booking.UserId && x.Type == "BookingNoShow"));
    }

    private async Task RunProcessorAsync()
    {
        await using var db = CreateDbContext();
        var processor = new BookingLifecycleProcessor(
            db,
            Options.Create(new BookingLifecycleJobOptions
            {
                IntervalSeconds = 60,
                NoShowGraceMinutes = 10,
                AutoStartEnabled = true,
                AutoCompleteEnabled = true
            }));

        await processor.ProcessAsync(CancellationToken.None);
    }

    private async Task SeedBookingAsync(Booking booking)
    {
        await using var db = CreateDbContext();
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
    }

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
}

[CollectionDefinition("BookingLifecycleProcessorTests", DisableParallelization = true)]
public sealed class BookingLifecycleProcessorCollection;
