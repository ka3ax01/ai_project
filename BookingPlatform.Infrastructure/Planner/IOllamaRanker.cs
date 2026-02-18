namespace BookingPlatform.Infrastructure.Planner;

public interface IOllamaRanker
{
    Task<IReadOnlyList<OllamaRankedItem>> RankAsync(OllamaRankingRequest request, CancellationToken cancellationToken);
}

public sealed class OllamaRankingRequest
{
    public string RequestedSummary { get; set; } = string.Empty;
    public double PriorityTime { get; set; }
    public double PriorityRoomProximity { get; set; }
    public double PriorityBuilding { get; set; }
    public IReadOnlyList<OllamaCandidate> Candidates { get; set; } = Array.Empty<OllamaCandidate>();
}

public sealed class OllamaCandidate
{
    public string CandidateId { get; set; } = string.Empty;
    public string BuildingCode { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public string StartTimeUtc { get; set; } = string.Empty;
    public string EndTimeUtc { get; set; } = string.Empty;
    public double Score { get; set; }
    public double RiskProbability { get; set; }
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
}

public sealed class OllamaRankedItem
{
    public string CandidateId { get; set; } = string.Empty;
    public IReadOnlyList<string> Reasons { get; set; } = Array.Empty<string>();
}
