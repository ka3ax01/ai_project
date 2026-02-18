namespace BookingPlatform.Application.Bookings;

public sealed class BookingRiskDto
{
    public Guid BookingId { get; set; }
    public double Probability { get; set; }
    public double Threshold { get; set; }
    public bool PredictedNoShow { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public string FeaturesJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
