using BookingPlatform.Application.Planner;

namespace BookingPlatform.Infrastructure.Planner;

public interface IAlternativeSlotSuggester
{
    Task<PlannerSuggestResponse> SuggestAsync(PlannerSuggestRequest request, Guid currentUserId, CancellationToken cancellationToken);
}
