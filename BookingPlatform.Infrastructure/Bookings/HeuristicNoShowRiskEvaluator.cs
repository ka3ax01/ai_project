using System.Text.Json;
using BookingPlatform.Application.Bookings;

namespace BookingPlatform.Infrastructure.Bookings;

public sealed class HeuristicNoShowRiskEvaluator : INoShowRiskEvaluator
{
    public Task<BookingRiskEvaluationResult> EvaluateAsync(BookingRiskContext context, CancellationToken cancellationToken)
    {
        var score = 0.10d;
        var features = new Dictionary<string, object?>
        {
            ["historicalNoShows"] = context.HistoricalNoShowCount,
            ["createdLessThan2HoursBeforeStart"] = false,
            ["durationOver2Hours"] = false,
            ["eveningUtcStart"] = false
        };

        if (context.HistoricalNoShowCount >= 2)
        {
            score += 0.30d;
        }

        var leadTimeHours = (context.StartTimeUtc - context.EvaluatedAtUtc).TotalHours;
        if (leadTimeHours < 2d)
        {
            score += 0.20d;
            features["createdLessThan2HoursBeforeStart"] = true;
        }

        var durationHours = (context.EndTimeUtc - context.StartTimeUtc).TotalHours;
        if (durationHours > 2d)
        {
            score += 0.10d;
            features["durationOver2Hours"] = true;
        }

        var hour = context.StartTimeUtc.UtcDateTime.Hour;
        if (hour >= 18 && hour <= 23)
        {
            score += 0.10d;
            features["eveningUtcStart"] = true;
        }

        score = Math.Clamp(score, 0d, 0.95d);

        return Task.FromResult(new BookingRiskEvaluationResult
        {
            Probability = score,
            ModelVersion = "heuristic-v1",
            FeaturesJson = JsonSerializer.Serialize(features)
        });
    }
}
