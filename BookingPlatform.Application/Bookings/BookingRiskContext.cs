namespace BookingPlatform.Application.Bookings;

public sealed class BookingRiskContext
{
    public Guid UserId { get; init; }
    public Guid RoomId { get; init; }
    public DateTimeOffset StartTimeUtc { get; init; }
    public DateTimeOffset EndTimeUtc { get; init; }
    public int HistoricalNoShowCount { get; init; }
    public DateTimeOffset EvaluatedAtUtc { get; init; }
}
