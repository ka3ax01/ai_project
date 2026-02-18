namespace BookingPlatform.Application.Planner;

public sealed class AlternativeSlotDto
{
    public string CandidateId { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public Guid BuildingId { get; set; }
    public string BuildingCode { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public DateTimeOffset StartTimeUtc { get; set; }
    public DateTimeOffset EndTimeUtc { get; set; }
    public double Score { get; set; }
    public double RiskProbability { get; set; }
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
}
