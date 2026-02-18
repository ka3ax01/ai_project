namespace BookingPlatform.Application.Planner;

public interface ISchedulePlanner
{
    Task<ScheduleProposalDto> ProposeAsync(PlannerRequest request, CancellationToken cancellationToken);
}
