namespace BookingPlatform.Infrastructure.Bookings;

public sealed class BookingLifecycleOptions
{
    public int NoShowGraceMinutes { get; set; } = 10;
}
