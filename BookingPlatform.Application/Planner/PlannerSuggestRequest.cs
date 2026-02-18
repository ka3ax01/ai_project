namespace BookingPlatform.Application.Planner;

public sealed class PlannerSuggestRequest
{
    public Guid RoomId { get; set; }
    public DateTimeOffset StartTimeUtc { get; set; }
    public DateTimeOffset EndTimeUtc { get; set; }
    public PlannerPriorityDto Priority { get; set; } = new();
    public PlannerConstraintsDto Constraints { get; set; } = new();
    public string Mode { get; set; } = "deterministic";
}

public sealed class PlannerPriorityDto
{
    public double Time { get; set; } = 0.6;
    public double RoomProximity { get; set; } = 0.3;
    public double Building { get; set; } = 0.1;
}

public sealed class PlannerConstraintsDto
{
    public bool SameDayOnly { get; set; } = true;
    public int MaxResults { get; set; } = 8;
    public int MaxTimeShiftMinutes { get; set; } = 180;
}
