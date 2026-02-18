using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookingPlatform.Infrastructure.Bookings;

public sealed class BookingLifecycleHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingLifecycleHostedService> _logger;
    private readonly BookingLifecycleJobOptions _options;

    public BookingLifecycleHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingLifecycleHostedService> logger,
        IOptions<BookingLifecycleJobOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _options.IntervalSeconds <= 0 ? 60 : _options.IntervalSeconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IBookingLifecycleProcessor>();
                var result = await processor.ProcessAsync(stoppingToken);

                _logger.LogInformation(
                    "Lifecycle run: expired={Expired}, started={Started}, completed={Completed}, noShow={NoShow}, total={Total}",
                    result.ExpiredPendingCount,
                    result.AutoStartedCount,
                    result.AutoCompletedCount,
                    result.NoShowCount,
                    result.Total);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Booking lifecycle hosted service iteration failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
}
