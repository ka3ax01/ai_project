namespace BookingPlatform.Application.Planner;

public sealed class ScheduleProposalDto
{
    public Guid RoomId { get; set; }
    public DateTimeOffset RequestedStartUtc { get; set; }
    public DateTimeOffset RequestedEndUtc { get; set; }
    public IReadOnlyList<ScheduleAlternativeDto> Alternatives { get; set; } = Array.Empty<ScheduleAlternativeDto>();
}

public sealed class ScheduleAlternativeDto
{
    public DateTimeOffset StartTimeUtc { get; set; }
    public DateTimeOffset EndTimeUtc { get; set; }
    public double Probability { get; set; }
    public double AvailabilityScore { get; set; }
    public double UtilizationScore { get; set; }
    public double FinalScore { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
}
