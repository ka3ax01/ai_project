namespace BookingPlatform.Infrastructure.Bookings;

public sealed class RiskPolicyOptions
{
    public double ConfirmationThreshold { get; set; } = 0.5;
    public int ConfirmationWindowMinutes { get; set; } = 15;
}
