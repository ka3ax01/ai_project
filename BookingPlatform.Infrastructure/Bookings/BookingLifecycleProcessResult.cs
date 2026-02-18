namespace BookingPlatform.Infrastructure.Bookings;

public sealed class BookingLifecycleProcessResult
{
    public int ExpiredPendingCount { get; set; }
    public int AutoStartedCount { get; set; }
    public int AutoCompletedCount { get; set; }
    public int NoShowCount { get; set; }

    public int Total => ExpiredPendingCount + AutoStartedCount + AutoCompletedCount + NoShowCount;
}
