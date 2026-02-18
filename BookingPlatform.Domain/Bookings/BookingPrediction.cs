using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Bookings;

public sealed class BookingPrediction : Entity
{
    public Guid BookingId { get; set; }
    public double Probability { get; set; }
    public double Threshold { get; set; }
    public bool PredictedLabel { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public string FeaturesJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
