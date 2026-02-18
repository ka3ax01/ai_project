namespace BookingPlatform.Application.Planner;

public sealed class PlannerRequest
{
    public Guid RoomId { get; set; }
    public DateTimeOffset RequestedStartUtc { get; set; }
    public DateTimeOffset RequestedEndUtc { get; set; }
}
