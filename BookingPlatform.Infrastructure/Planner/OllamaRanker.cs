using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BookingPlatform.Infrastructure.Planner;

public sealed class OllamaRanker : IOllamaRanker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    public OllamaRanker(HttpClient httpClient, IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<OllamaRankedItem>> RankAsync(OllamaRankingRequest request, CancellationToken cancellationToken)
    {
        if (request.Candidates.Count == 0)
        {
            return Array.Empty<OllamaRankedItem>();
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds <= 0 ? 5 : _options.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var prompt = BuildPrompt(request);
        var body = new
        {
            model = string.IsNullOrWhiteSpace(_options.Model) ? "llama3" : _options.Model,
            stream = false,
            messages = new[]
            {
                new { role = "system", content = "Return JSON only. No markdown. No prose outside JSON." },
                new { role = "user", content = prompt }
            }
        };

        using var response = await _httpClient.PostAsJsonAsync("/api/chat", body, JsonOptions, linkedCts.Token);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: linkedCts.Token);

        if (!document.RootElement.TryGetProperty("message", out var messageElement) ||
            !messageElement.TryGetProperty("content", out var contentElement))
        {
            throw new InvalidOperationException("Invalid Ollama response format.");
        }

        var content = contentElement.GetString();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Empty Ollama content.");
        }

        using var rankedDoc = JsonDocument.Parse(content);
        if (!rankedDoc.RootElement.TryGetProperty("ordered", out var orderedElement) ||
            orderedElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Invalid Ollama ranked payload.");
        }

        var ranked = new List<OllamaRankedItem>();
        foreach (var item in orderedElement.EnumerateArray())
        {
            if (!item.TryGetProperty("candidateId", out var idElement))
            {
                continue;
            }

            var candidateId = idElement.GetString();
            if (string.IsNullOrWhiteSpace(candidateId))
            {
                continue;
            }

            var reasons = new List<string>();
            if (item.TryGetProperty("reasons", out var reasonsElement) && reasonsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var reason in reasonsElement.EnumerateArray())
                {
                    var reasonText = reason.GetString();
                    if (!string.IsNullOrWhiteSpace(reasonText))
                    {
                        reasons.Add(reasonText);
                    }
                }
            }

            ranked.Add(new OllamaRankedItem
            {
                CandidateId = candidateId,
                Reasons = reasons
            });
        }

        return ranked;
    }

    private static string BuildPrompt(OllamaRankingRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Rank the given room booking candidates.");
        sb.AppendLine("You MUST NOT invent candidates.");
        sb.AppendLine("Return ONLY JSON with this exact shape:");
        sb.AppendLine("{\"ordered\":[{\"candidateId\":\"...\",\"reasons\":[\"...\"]}]}");
        sb.AppendLine("Candidate IDs in output must be subset of provided IDs.");
        sb.AppendLine($"Requested: {request.RequestedSummary}");
        sb.AppendLine($"Priority weights: time={request.PriorityTime:0.###}, roomProximity={request.PriorityRoomProximity:0.###}, building={request.PriorityBuilding:0.###}");
        sb.AppendLine("Candidates:");
        foreach (var c in request.Candidates)
        {
            sb.AppendLine($"- candidateId={c.CandidateId}; building={c.BuildingCode}; room={c.RoomNumber}; floor={c.Floor}; start={c.StartTimeUtc}; end={c.EndTimeUtc}; score={c.Score:0.###}; risk={c.RiskProbability:0.###}; reasons={string.Join(" | ", c.Reasons)}");
        }

        return sb.ToString();
    }
}
