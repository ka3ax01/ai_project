namespace BookingPlatform.Infrastructure.Bookings;

public sealed class BookingLifecycleOptions
{
    public int ConfirmationWindowMinutes { get; set; } = 15;
    public int NoShowGraceMinutes { get; set; } = 10;
}
