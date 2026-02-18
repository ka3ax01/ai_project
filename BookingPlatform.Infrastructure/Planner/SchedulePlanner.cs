using BookingPlatform.Application.Bookings;
using BookingPlatform.Application.Common;
using BookingPlatform.Application.Planner;
using BookingPlatform.Domain.Enums;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Infrastructure.Planner;

public sealed class SchedulePlanner : ISchedulePlanner
{
    private readonly AppDbContext _dbContext;
    private readonly IRequestContextAccessor _requestContextAccessor;
    private readonly INoShowRiskEvaluator _riskEvaluator;

    public SchedulePlanner(
        AppDbContext dbContext,
        IRequestContextAccessor requestContextAccessor,
        INoShowRiskEvaluator riskEvaluator)
    {
        _dbContext = dbContext;
        _requestContextAccessor = requestContextAccessor;
        _riskEvaluator = riskEvaluator;
    }

    public async Task<ScheduleProposalDto> ProposeAsync(PlannerRequest request, CancellationToken cancellationToken)
    {
        var userId = _requestContextAccessor.UserId
                     ?? throw new InvalidOperationException("Current user is not resolved.");

        var requestedStartUtc = request.RequestedStartUtc.ToUniversalTime();
        var requestedEndUtc = request.RequestedEndUtc.ToUniversalTime();
        if (requestedStartUtc >= requestedEndUtc)
        {
            throw new InvalidOperationException("RequestedStartUtc must be less than RequestedEndUtc.");
        }

        var duration = requestedEndUtc - requestedStartUtc;
        var candidates = BuildCandidates(requestedStartUtc, duration);

        var noShowCount = await _dbContext.Bookings
            .AsNoTracking()
            .CountAsync(
                b => b.UserId == userId && b.Status == BookingStatus.NoShow,
                cancellationToken);

        var utilizationByDay = await BuildUtilizationByDayAsync(request.RoomId, candidates, cancellationToken);

        var blockingStatuses = new[]
        {
            BookingStatus.Pending,
            BookingStatus.Confirmed,
            BookingStatus.InProgress
        };

        var alternatives = new List<ScheduleAlternativeDto>(candidates.Count);

        foreach (var candidateStart in candidates)
        {
            var candidateEnd = candidateStart + duration;

            var hasConflict = await _dbContext.Bookings
                .AsNoTracking()
                .AnyAsync(
                    b => b.RoomId == request.RoomId
                         && blockingStatuses.Contains(b.Status)
                         && b.StartTimeUtc < candidateEnd
                         && b.EndTimeUtc > candidateStart,
                    cancellationToken);

            var evaluation = await _riskEvaluator.EvaluateAsync(
                new BookingRiskContext
                {
                    UserId = userId,
                    RoomId = request.RoomId,
                    StartTimeUtc = candidateStart,
                    EndTimeUtc = candidateEnd,
                    HistoricalNoShowCount = noShowCount,
                    EvaluatedAtUtc = DateTimeOffset.UtcNow
                },
                cancellationToken);

            var availabilityScore = hasConflict ? 0d : 1d;
            var utilizationScore = utilizationByDay[candidateStart.UtcDateTime.Date];
            var finalScore =
                ((1d - evaluation.Probability) * 0.5d) +
                (availabilityScore * 0.3d) +
                (utilizationScore * 0.2d);

            var reasons = new List<string>(4)
            {
                hasConflict
                    ? "Conflicts with an existing booking."
                    : "No overlap with active bookings.",
                $"Predicted no-show probability is {evaluation.Probability:P0}.",
                $"Room utilization for the day is {utilizationScore:P0}.",
                availabilityScore > 0d
                    ? "Availability contributes positively to ranking."
                    : "Availability penalty applied due to conflict."
            };

            alternatives.Add(new ScheduleAlternativeDto
            {
                StartTimeUtc = candidateStart,
                EndTimeUtc = candidateEnd,
                Probability = evaluation.Probability,
                AvailabilityScore = availabilityScore,
                UtilizationScore = utilizationScore,
                FinalScore = finalScore,
                ModelVersion = evaluation.ModelVersion,
                Reasons = reasons
            });
        }

        var top = alternatives
            .OrderByDescending(x => x.FinalScore)
            .ThenBy(x => x.StartTimeUtc)
            .Take(5)
            .ToList();

        return new ScheduleProposalDto
        {
            RoomId = request.RoomId,
            RequestedStartUtc = requestedStartUtc,
            RequestedEndUtc = requestedEndUtc,
            Alternatives = top
        };
    }

    private static List<DateTimeOffset> BuildCandidates(DateTimeOffset requestedStartUtc, TimeSpan duration)
    {
        var result = new List<DateTimeOffset>();
        var first = requestedStartUtc.AddHours(-2);
        var last = requestedStartUtc.AddHours(2);

        for (var cursor = first; cursor <= last; cursor = cursor.AddMinutes(30))
        {
            var end = cursor.Add(duration);
            var startUtc = cursor.ToUniversalTime();
            var endUtc = end.ToUniversalTime();

            var day = startUtc.UtcDateTime.Date;
            var workStart = new DateTimeOffset(day, TimeSpan.Zero);
            var workEnd = workStart.AddHours(20);
            var workBegin = workStart.AddHours(8);

            if (startUtc < workBegin || endUtc > workEnd)
            {
                continue;
            }

            result.Add(startUtc);
        }

        return result;
    }

    private async Task<Dictionary<DateTime, double>> BuildUtilizationByDayAsync(
        Guid roomId,
        IReadOnlyList<DateTimeOffset> candidates,
        CancellationToken cancellationToken)
    {
        var utilizationByDay = new Dictionary<DateTime, double>();
        var dayKeys = candidates
            .Select(c => c.UtcDateTime.Date)
            .Distinct()
            .ToList();

        var utilizationStatuses = new[]
        {
            BookingStatus.Pending,
            BookingStatus.Confirmed,
            BookingStatus.InProgress,
            BookingStatus.Completed
        };

        foreach (var day in dayKeys)
        {
            var dayStart = new DateTimeOffset(day, TimeSpan.Zero);
            var dayEnd = dayStart.AddDays(1);

            var bookings = await _dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.RoomId == roomId
                            && utilizationStatuses.Contains(b.Status)
                            && b.StartTimeUtc < dayEnd
                            && b.EndTimeUtc > dayStart)
                .Select(b => new { b.StartTimeUtc, b.EndTimeUtc })
                .ToListAsync(cancellationToken);

            var totalHours = 0d;
            foreach (var booking in bookings)
            {
                var from = booking.StartTimeUtc > dayStart ? booking.StartTimeUtc : dayStart;
                var to = booking.EndTimeUtc < dayEnd ? booking.EndTimeUtc : dayEnd;
                if (to > from)
                {
                    totalHours += (to - from).TotalHours;
                }
            }

            utilizationByDay[day] = Math.Clamp(totalHours / 8d, 0d, 1d);
        }

        return utilizationByDay;
    }
}
