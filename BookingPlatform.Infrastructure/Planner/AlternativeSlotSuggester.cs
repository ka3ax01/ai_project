using System.Security.Cryptography;
using System.Text;
using BookingPlatform.Application.Bookings;
using BookingPlatform.Application.Planner;
using BookingPlatform.Domain.Enums;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BookingPlatform.Infrastructure.Planner;

public sealed class AlternativeSlotSuggester : IAlternativeSlotSuggester
{
    private readonly AppDbContext _dbContext;
    private readonly INoShowRiskEvaluator _riskEvaluator;
    private readonly IOllamaRanker _ollamaRanker;
    private readonly PlannerOptions _plannerOptions;

    public AlternativeSlotSuggester(
        AppDbContext dbContext,
        INoShowRiskEvaluator riskEvaluator,
        IOllamaRanker ollamaRanker,
        IOptions<PlannerOptions> plannerOptions)
    {
        _dbContext = dbContext;
        _riskEvaluator = riskEvaluator;
        _ollamaRanker = ollamaRanker;
        _plannerOptions = plannerOptions.Value;
    }

    public async Task<PlannerSuggestResponse> SuggestAsync(
        PlannerSuggestRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var startUtc = request.StartTimeUtc.ToUniversalTime();
        var endUtc = request.EndTimeUtc.ToUniversalTime();
        if (startUtc >= endUtc)
        {
            throw new InvalidOperationException("StartTimeUtc must be less than EndTimeUtc.");
        }

        var requestedRoom = await _dbContext.Rooms
            .AsNoTracking()
            .Join(
                _dbContext.Buildings.AsNoTracking(),
                r => r.BuildingId,
                b => b.Id,
                (r, b) => new RoomInfo
                {
                    RoomId = r.Id,
                    BuildingId = r.BuildingId,
                    BuildingCode = b.Code,
                    BuildingName = b.Name,
                    RoomNumber = r.Number,
                    Floor = r.Floor,
                    IsActive = r.IsActive
                })
            .FirstOrDefaultAsync(x => x.RoomId == request.RoomId, cancellationToken);

        if (requestedRoom is null)
        {
            throw new KeyNotFoundException("Requested room not found.");
        }

        if (!requestedRoom.IsActive)
        {
            throw new InvalidOperationException("Requested room is not active.");
        }

        var duration = endUtc - startUtc;
        var constraints = request.Constraints ?? new PlannerConstraintsDto();
        var maxResults = Math.Clamp(constraints.MaxResults <= 0 ? 8 : constraints.MaxResults, 1, 20);
        var maxShiftMinutes = Math.Max(0, constraints.MaxTimeShiftMinutes <= 0 ? 180 : constraints.MaxTimeShiftMinutes);
        var sameDayOnly = constraints.SameDayOnly;

        var workingDay = BuildWorkingDay(startUtc.Date, duration);
        var timeBuckets = BuildTimeBuckets(startUtc, duration, maxShiftMinutes, sameDayOnly, workingDay);
        if (timeBuckets.Count == 0)
        {
            return EmptyResponse(request, "No free slots on this day.");
        }

        var rooms = await _dbContext.Rooms
            .AsNoTracking()
            .Where(r => r.IsActive)
            .Join(
                _dbContext.Buildings.AsNoTracking(),
                r => r.BuildingId,
                b => b.Id,
                (r, b) => new RoomInfo
                {
                    RoomId = r.Id,
                    BuildingId = r.BuildingId,
                    BuildingCode = b.Code,
                    BuildingName = b.Name,
                    RoomNumber = r.Number,
                    Floor = r.Floor,
                    IsActive = r.IsActive
                })
            .ToListAsync(cancellationToken);

        var blockingStatuses = new[] { BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.InProgress };
        var orderedRoomsExcludingRequested = OrderRoomsByPriority(rooms, requestedRoom, includeRequestedRoom: false);
        var orderedRoomsIncludingRequested = OrderRoomsByPriority(rooms, requestedRoom, includeRequestedRoom: true);

        var maxCandidatePool = _plannerOptions.MaxCandidatePool <= 0 ? 30 : _plannerOptions.MaxCandidatePool;
        var candidatePool = new List<CandidateWorkItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var timeStart in timeBuckets)
        {
            var timeEnd = timeStart + duration;
            var roomOrder = timeStart == startUtc ? orderedRoomsExcludingRequested : orderedRoomsIncludingRequested;

            foreach (var room in roomOrder)
            {
                var key = $"{room.RoomId:N}:{timeStart.UtcTicks}:{timeEnd.UtcTicks}";
                if (!seen.Add(key))
                {
                    continue;
                }

                var hasConflict = await _dbContext.Bookings
                    .AsNoTracking()
                    .AnyAsync(
                        b => b.RoomId == room.RoomId
                             && blockingStatuses.Contains(b.Status)
                             && b.StartTimeUtc < timeEnd
                             && b.EndTimeUtc > timeStart,
                        cancellationToken);

                if (hasConflict)
                {
                    continue;
                }

                candidatePool.Add(new CandidateWorkItem
                {
                    Room = room,
                    StartTimeUtc = timeStart,
                    EndTimeUtc = timeEnd
                });

                if (candidatePool.Count >= maxCandidatePool)
                {
                    break;
                }
            }

            if (candidatePool.Count >= maxCandidatePool)
            {
                break;
            }
        }

        if (candidatePool.Count == 0)
        {
            return EmptyResponse(request, "No free slots on this day.");
        }

        var noShowCount = await _dbContext.Bookings
            .AsNoTracking()
            .CountAsync(b => b.UserId == currentUserId && b.Status == BookingStatus.NoShow, cancellationToken);

        var normalized = NormalizeWeights(request.Priority ?? new PlannerPriorityDto());
        var utilizationCache = new Dictionary<(Guid RoomId, DateTime Day), double>();
        var deterministic = new List<AlternativeSlotDto>(candidatePool.Count);

        foreach (var candidate in candidatePool)
        {
            var risk = await _riskEvaluator.EvaluateAsync(
                new BookingRiskContext
                {
                    UserId = currentUserId,
                    RoomId = candidate.Room.RoomId,
                    StartTimeUtc = candidate.StartTimeUtc,
                    EndTimeUtc = candidate.EndTimeUtc,
                    HistoricalNoShowCount = noShowCount,
                    EvaluatedAtUtc = DateTimeOffset.UtcNow
                },
                cancellationToken);

            var utilizationScore = await GetUtilizationScoreAsync(
                candidate.Room.RoomId,
                candidate.StartTimeUtc.UtcDateTime.Date,
                utilizationCache,
                cancellationToken);

            var timeShiftMinutes = (int)Math.Round((candidate.StartTimeUtc - startUtc).TotalMinutes);
            var absShiftMinutes = Math.Abs(timeShiftMinutes);
            var timeComponent = absShiftMinutes == 0
                ? 1d
                : maxShiftMinutes <= 0
                    ? 0d
                    : Math.Max(0d, 1d - (absShiftMinutes / (double)maxShiftMinutes));

            var sameBuilding = candidate.Room.BuildingId == requestedRoom.BuildingId;
            var sameFloor = sameBuilding && candidate.Room.Floor == requestedRoom.Floor;
            var roomComponent = sameFloor ? 1d : sameBuilding ? 0.7d : 0.4d;
            var buildingComponent = sameBuilding ? 1d : 0d;

            var riskScore = 1d - risk.Probability;
            const double availabilityScore = 1d;

            var finalScore =
                (riskScore * 0.4d) +
                (availabilityScore * 0.1d) +
                (timeComponent * normalized.Time) +
                (roomComponent * normalized.RoomProximity) +
                (buildingComponent * normalized.Building);

            var reasons = new List<string>
            {
                timeShiftMinutes == 0
                    ? "Same time slot"
                    : $"Time shift: {(timeShiftMinutes > 0 ? "+" : string.Empty)}{timeShiftMinutes} min",
                sameFloor
                    ? "Same building & floor"
                    : sameBuilding
                        ? "Same building"
                        : "Different building",
                $"Predicted no-show risk: {risk.Probability:0.00}",
                $"Daily utilization: {utilizationScore:0.00}",
                "No conflicts"
            };

            deterministic.Add(new AlternativeSlotDto
            {
                CandidateId = CreateStableCandidateId(candidate.Room.RoomId, candidate.StartTimeUtc, candidate.EndTimeUtc),
                RoomId = candidate.Room.RoomId,
                BuildingId = candidate.Room.BuildingId,
                BuildingCode = candidate.Room.BuildingCode,
                BuildingName = candidate.Room.BuildingName,
                RoomNumber = candidate.Room.RoomNumber,
                Floor = candidate.Room.Floor,
                StartTimeUtc = candidate.StartTimeUtc,
                EndTimeUtc = candidate.EndTimeUtc,
                Score = finalScore,
                RiskProbability = risk.Probability,
                Reasons = reasons
            });
        }

        var ordered = deterministic
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.StartTimeUtc)
            .ToList();

        if (string.Equals(request.Mode, "ollama", StringComparison.OrdinalIgnoreCase))
        {
            ordered = await TryRankWithOllamaAsync(request, ordered, normalized, cancellationToken);
        }

        return new PlannerSuggestResponse
        {
            Requested = new PlannerSuggestRequestedDto
            {
                RoomId = request.RoomId,
                StartTimeUtc = startUtc,
                EndTimeUtc = endUtc
            },
            Results = ordered.Take(maxResults).ToList(),
            Message = null
        };
    }

    private async Task<List<AlternativeSlotDto>> TryRankWithOllamaAsync(
        PlannerSuggestRequest request,
        List<AlternativeSlotDto> deterministic,
        NormalizedWeights weights,
        CancellationToken cancellationToken)
    {
        try
        {
            var rankingRequest = new OllamaRankingRequest
            {
                RequestedSummary = $"roomId={request.RoomId}, start={request.StartTimeUtc:O}, end={request.EndTimeUtc:O}",
                PriorityTime = weights.Time,
                PriorityRoomProximity = weights.RoomProximity,
                PriorityBuilding = weights.Building,
                Candidates = deterministic
                    .Take(30)
                    .Select(x => new OllamaCandidate
                    {
                        CandidateId = x.CandidateId,
                        BuildingCode = x.BuildingCode,
                        RoomNumber = x.RoomNumber,
                        Floor = x.Floor,
                        StartTimeUtc = x.StartTimeUtc.ToString("O"),
                        EndTimeUtc = x.EndTimeUtc.ToString("O"),
                        Score = x.Score,
                        RiskProbability = x.RiskProbability,
                        Reasons = x.Reasons
                    })
                    .ToList()
            };

            var ranked = await _ollamaRanker.RankAsync(rankingRequest, cancellationToken);
            if (ranked.Count == 0)
            {
                return deterministic;
            }

            var map = deterministic.ToDictionary(x => x.CandidateId, StringComparer.OrdinalIgnoreCase);
            if (ranked.Any(item => !map.ContainsKey(item.CandidateId)))
            {
                return deterministic;
            }

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<AlternativeSlotDto>(deterministic.Count);

            foreach (var item in ranked)
            {
                if (!map.TryGetValue(item.CandidateId, out var candidate))
                {
                    continue;
                }

                if (!used.Add(item.CandidateId))
                {
                    continue;
                }

                if (item.Reasons.Count > 0)
                {
                    candidate = CloneWithAiReasons(candidate, item.Reasons);
                }

                ordered.Add(candidate);
            }

            foreach (var candidate in deterministic)
            {
                if (used.Add(candidate.CandidateId))
                {
                    ordered.Add(candidate);
                }
            }

            return ordered;
        }
        catch
        {
            return deterministic;
        }
    }

    private static AlternativeSlotDto CloneWithAiReasons(AlternativeSlotDto source, IReadOnlyList<string> aiReasons)
    {
        var mergedReasons = aiReasons
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(3)
            .Concat(source.Reasons)
            .Distinct()
            .ToList();

        return new AlternativeSlotDto
        {
            CandidateId = source.CandidateId,
            RoomId = source.RoomId,
            BuildingId = source.BuildingId,
            BuildingCode = source.BuildingCode,
            BuildingName = source.BuildingName,
            RoomNumber = source.RoomNumber,
            Floor = source.Floor,
            StartTimeUtc = source.StartTimeUtc,
            EndTimeUtc = source.EndTimeUtc,
            Score = source.Score,
            RiskProbability = source.RiskProbability,
            Reasons = mergedReasons
        };
    }

    private PlannerSuggestResponse EmptyResponse(PlannerSuggestRequest request, string message) => new()
    {
        Requested = new PlannerSuggestRequestedDto
        {
            RoomId = request.RoomId,
            StartTimeUtc = request.StartTimeUtc.ToUniversalTime(),
            EndTimeUtc = request.EndTimeUtc.ToUniversalTime()
        },
        Results = Array.Empty<AlternativeSlotDto>(),
        Message = message
    };

    private async Task<double> GetUtilizationScoreAsync(
        Guid roomId,
        DateTime dayUtc,
        Dictionary<(Guid RoomId, DateTime Day), double> cache,
        CancellationToken cancellationToken)
    {
        var key = (roomId, dayUtc);
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var dayStart = new DateTimeOffset(dayUtc, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var statuses = new[]
        {
            BookingStatus.Pending,
            BookingStatus.Confirmed,
            BookingStatus.InProgress,
            BookingStatus.Completed
        };

        var bookings = await _dbContext.Bookings
            .AsNoTracking()
            .Where(b => b.RoomId == roomId
                        && statuses.Contains(b.Status)
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

        var score = Math.Clamp(totalHours / 8d, 0d, 1d);
        cache[key] = score;
        return score;
    }

    private List<RoomInfo> OrderRoomsByPriority(IReadOnlyList<RoomInfo> rooms, RoomInfo requestedRoom, bool includeRequestedRoom)
    {
        var requestedNumber = ExtractNumber(requestedRoom.RoomNumber);

        var sameBuildingSameFloor = rooms
            .Where(r => r.BuildingId == requestedRoom.BuildingId
                        && r.Floor == requestedRoom.Floor
                        && (includeRequestedRoom || r.RoomId != requestedRoom.RoomId))
            .OrderBy(r => RoomDistance(r.RoomNumber, requestedRoom.RoomNumber, requestedNumber))
            .ThenBy(r => r.RoomNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sameBuildingOtherFloors = rooms
            .Where(r => r.BuildingId == requestedRoom.BuildingId
                        && r.Floor != requestedRoom.Floor
                        && r.RoomId != requestedRoom.RoomId)
            .OrderBy(r => Math.Abs(r.Floor - requestedRoom.Floor))
            .ThenBy(r => RoomDistance(r.RoomNumber, requestedRoom.RoomNumber, requestedNumber))
            .ThenBy(r => r.RoomNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var otherBuildings = rooms
            .Where(r => r.BuildingId != requestedRoom.BuildingId
                        && r.RoomId != requestedRoom.RoomId)
            .OrderBy(r => r.BuildingCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => Math.Abs(r.Floor - requestedRoom.Floor))
            .ThenBy(r => RoomDistance(r.RoomNumber, requestedRoom.RoomNumber, requestedNumber))
            .ThenBy(r => r.RoomNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return sameBuildingSameFloor
            .Concat(sameBuildingOtherFloors)
            .Concat(otherBuildings)
            .ToList();
    }

    private List<DateTimeOffset> BuildTimeBuckets(
        DateTimeOffset requestedStartUtc,
        TimeSpan duration,
        int maxShiftMinutes,
        bool sameDayOnly,
        WorkingDay workingDay)
    {
        var stepMinutes = _plannerOptions.SlotStepMinutes <= 0 ? 30 : _plannerOptions.SlotStepMinutes;
        var seen = new HashSet<long>();
        var result = new List<DateTimeOffset>();

        void TryAdd(DateTimeOffset value)
        {
            var candidate = value.ToUniversalTime();
            var candidateEnd = candidate + duration;
            if (sameDayOnly && candidate.UtcDateTime.Date != requestedStartUtc.UtcDateTime.Date)
            {
                return;
            }

            if (candidate < workingDay.Start || candidateEnd > workingDay.End)
            {
                return;
            }

            if (!seen.Add(candidate.UtcTicks))
            {
                return;
            }

            result.Add(candidate);
        }

        TryAdd(requestedStartUtc);

        for (var delta = stepMinutes; delta <= maxShiftMinutes; delta += stepMinutes)
        {
            TryAdd(requestedStartUtc.AddMinutes(delta));
            TryAdd(requestedStartUtc.AddMinutes(-delta));
        }

        for (var cursor = workingDay.Start; cursor <= workingDay.LatestStart; cursor = cursor.AddMinutes(stepMinutes))
        {
            TryAdd(cursor);
        }

        return result;
    }

    private WorkingDay BuildWorkingDay(DateTime dayUtc, TimeSpan duration)
    {
        var startHour = Math.Clamp(_plannerOptions.WorkingDayStartHourUtc, 0, 23);
        var endHour = Math.Clamp(_plannerOptions.WorkingDayEndHourUtc, 1, 24);
        if (endHour <= startHour)
        {
            endHour = Math.Min(startHour + 1, 24);
        }

        var dayStart = new DateTimeOffset(dayUtc, TimeSpan.Zero).AddHours(startHour);
        var dayEnd = new DateTimeOffset(dayUtc, TimeSpan.Zero).AddHours(endHour);
        var latestStart = dayEnd - duration;

        return new WorkingDay(dayStart, dayEnd, latestStart);
    }

    private static int? ExtractNumber(string roomNumber)
    {
        var digits = new string(roomNumber.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : null;
    }

    private static int RoomDistance(string roomNumber, string requestedRoomNumber, int? requestedNumeric)
    {
        var numeric = ExtractNumber(roomNumber);
        if (numeric.HasValue && requestedNumeric.HasValue)
        {
            return Math.Abs(numeric.Value - requestedNumeric.Value);
        }

        return Math.Abs(string.Compare(roomNumber, requestedRoomNumber, StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateStableCandidateId(Guid roomId, DateTimeOffset start, DateTimeOffset end)
    {
        var raw = $"{roomId:N}|{start.UtcTicks}|{end.UtcTicks}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var guidBytes = bytes.Take(16).ToArray();
        return new Guid(guidBytes).ToString();
    }

    private static NormalizedWeights NormalizeWeights(PlannerPriorityDto priority)
    {
        var time = Math.Max(0d, priority.Time);
        var room = Math.Max(0d, priority.RoomProximity);
        var building = Math.Max(0d, priority.Building);
        var sum = time + room + building;

        if (sum <= 0d)
        {
            return new NormalizedWeights(0.6d, 0.3d, 0.1d);
        }

        return new NormalizedWeights(time / sum, room / sum, building / sum);
    }

    private sealed class CandidateWorkItem
    {
        public required RoomInfo Room { get; init; }
        public DateTimeOffset StartTimeUtc { get; init; }
        public DateTimeOffset EndTimeUtc { get; init; }
    }

    private sealed class RoomInfo
    {
        public Guid RoomId { get; init; }
        public Guid BuildingId { get; init; }
        public string BuildingCode { get; init; } = string.Empty;
        public string BuildingName { get; init; } = string.Empty;
        public string RoomNumber { get; init; } = string.Empty;
        public int Floor { get; init; }
        public bool IsActive { get; init; }
    }

    private readonly record struct NormalizedWeights(double Time, double RoomProximity, double Building);
    private readonly record struct WorkingDay(DateTimeOffset Start, DateTimeOffset End, DateTimeOffset LatestStart);
}
