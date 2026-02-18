namespace BookingPlatform.Application.Bookings;

public sealed class BookingRiskEvaluationResult
{
    public double Probability { get; init; }
    public string ModelVersion { get; init; } = string.Empty;
    public string FeaturesJson { get; init; } = "{}";
}
