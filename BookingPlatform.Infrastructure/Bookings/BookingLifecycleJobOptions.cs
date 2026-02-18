namespace BookingPlatform.Infrastructure.Bookings;

public sealed class BookingLifecycleJobOptions
{
    public int IntervalSeconds { get; set; } = 60;
    public int ConfirmationWindowMinutes { get; set; } = 15;
    public int NoShowGraceMinutes { get; set; } = 10;
    public bool AutoStartEnabled { get; set; } = true;
    public bool AutoCompleteEnabled { get; set; } = true;
}
