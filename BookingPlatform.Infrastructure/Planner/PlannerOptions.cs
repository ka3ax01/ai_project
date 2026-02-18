namespace BookingPlatform.Infrastructure.Planner;

public sealed class PlannerOptions
{
    public int WorkingDayStartHourUtc { get; set; } = 8;
    public int WorkingDayEndHourUtc { get; set; } = 20;
    public int SlotStepMinutes { get; set; } = 30;
    public int MaxCandidatePool { get; set; } = 30;
}
