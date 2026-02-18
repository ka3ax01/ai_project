namespace BookingPlatform.Application.Planner;

public sealed class PlannerSuggestResponse
{
    public PlannerSuggestRequestedDto Requested { get; set; } = new();
    public IReadOnlyList<AlternativeSlotDto> Results { get; set; } = Array.Empty<AlternativeSlotDto>();
    public string? Message { get; set; }
}

public sealed class PlannerSuggestRequestedDto
{
    public Guid RoomId { get; set; }
    public DateTimeOffset StartTimeUtc { get; set; }
    public DateTimeOffset EndTimeUtc { get; set; }
}
