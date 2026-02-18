using BookingPlatform.Application.Bookings;
using BookingPlatform.Application.Planner;
using BookingPlatform.Domain.Bookings;
using BookingPlatform.Domain.Buildings;
using BookingPlatform.Domain.Enums;
using BookingPlatform.Domain.Rooms;
using BookingPlatform.Infrastructure.Persistence;
using BookingPlatform.Infrastructure.Planner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace BookingPlatform.IntegrationTests;

[Collection("AlternativeSlotSuggesterTests")]
public sealed class AlternativeSlotSuggesterTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("booking_platform_suggester_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task SuggestAsync_ReturnsSameTimeSameFloor_WhenAvailable()
    {
        await ResetDatabaseAsync();
        var seed = await SeedBaseLayoutAsync();

        await SeedConflictAsync(seed.RequestedRoomId, seed.StartUtc, seed.EndUtc);

        var result = await RunSuggestAsync(seed.RequestedRoomId, "deterministic");

        Assert.NotEmpty(result.Results);
        var top = result.Results[0];
        Assert.Equal(seed.SameFloorRoomId, top.RoomId);
        Assert.Equal(seed.StartUtc, top.StartTimeUtc);
        Assert.Equal(seed.EndUtc, top.EndTimeUtc);
    }

    [Fact]
    public async Task SuggestAsync_FallsBackToSameBuildingOtherFloor_WhenSameFloorUnavailable()
    {
        await ResetDatabaseAsync();
        var seed = await SeedBaseLayoutAsync();

        await SeedConflictAsync(seed.RequestedRoomId, seed.StartUtc, seed.EndUtc);
        await SeedConflictAsync(seed.SameFloorRoomId, seed.StartUtc, seed.EndUtc);

        var result = await RunSuggestAsync(seed.RequestedRoomId, "deterministic");

        Assert.NotEmpty(result.Results);
        var top = result.Results[0];
        Assert.Equal(seed.OtherFloorRoomId, top.RoomId);
        Assert.Equal(seed.StartUtc, top.StartTimeUtc);
    }

    [Fact]
    public async Task SuggestAsync_FallsBackToOtherBuilding_WhenBuildingUnavailable()
    {
        await ResetDatabaseAsync();
        var seed = await SeedBaseLayoutAsync();

        await SeedConflictAsync(seed.RequestedRoomId, seed.StartUtc, seed.EndUtc);
        await SeedConflictAsync(seed.SameFloorRoomId, seed.StartUtc, seed.EndUtc);
        await SeedConflictAsync(seed.OtherFloorRoomId, seed.StartUtc, seed.EndUtc);

        var result = await RunSuggestAsync(seed.RequestedRoomId, "deterministic");

        Assert.NotEmpty(result.Results);
        var top = result.Results[0];
        Assert.Equal(seed.OtherBuildingRoomId, top.RoomId);
        Assert.Equal(seed.StartUtc, top.StartTimeUtc);
    }

    [Fact]
    public async Task SuggestAsync_FallsBackToOtherTime_WhenNoRoomsFreeAtSameTime()
    {
        await ResetDatabaseAsync();
        var seed = await SeedBaseLayoutAsync();

        await SeedConflictAsync(seed.RequestedRoomId, seed.StartUtc, seed.EndUtc);
        await SeedConflictAsync(seed.SameFloorRoomId, seed.StartUtc, seed.EndUtc);
        await SeedConflictAsync(seed.OtherFloorRoomId, seed.StartUtc, seed.EndUtc);
        await SeedConflictAsync(seed.OtherBuildingRoomId, seed.StartUtc, seed.EndUtc);

        var result = await RunSuggestAsync(seed.RequestedRoomId, "deterministic");

        Assert.NotEmpty(result.Results);
        var top = result.Results[0];
        Assert.NotEqual(seed.StartUtc, top.StartTimeUtc);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task SuggestAsync_ReturnsEmptyWithMessage_WhenNoSlotsFreeOnDay()
    {
        await ResetDatabaseAsync();
        var seed = await SeedBaseLayoutAsync();

        await SeedAllDayConflictAsync(seed.RequestedRoomId);
        await SeedAllDayConflictAsync(seed.SameFloorRoomId);
        await SeedAllDayConflictAsync(seed.OtherFloorRoomId);
        await SeedAllDayConflictAsync(seed.OtherBuildingRoomId);

        var result = await RunSuggestAsync(seed.RequestedRoomId, "deterministic");

        Assert.Empty(result.Results);
        Assert.Equal("No free slots on this day.", result.Message);
    }

    [Fact]
    public async Task SuggestAsync_OllamaModeInvalidIds_FallsBackToDeterministicOrder()
    {
        await ResetDatabaseAsync();
        var seed = await SeedBaseLayoutAsync();
        await SeedConflictAsync(seed.RequestedRoomId, seed.StartUtc, seed.EndUtc);

        var deterministic = await RunSuggestAsync(seed.RequestedRoomId, "deterministic");
        var ollama = await RunSuggestAsync(seed.RequestedRoomId, "ollama");

        Assert.NotEmpty(deterministic.Results);
        Assert.NotEmpty(ollama.Results);

        var deterministicIds = deterministic.Results.Select(x => x.CandidateId).ToArray();
        var ollamaIds = ollama.Results.Select(x => x.CandidateId).ToArray();
        Assert.Equal(deterministicIds, ollamaIds);
    }

    private async Task<PlannerSuggestResponse> RunSuggestAsync(Guid requestedRoomId, string mode)
    {
        await using var db = CreateDbContext();

        var service = new AlternativeSlotSuggester(
            db,
            new FakeRiskEvaluator(),
            new FakeInvalidOllamaRanker(),
            Options.Create(new PlannerOptions
            {
                WorkingDayStartHourUtc = 8,
                WorkingDayEndHourUtc = 20,
                SlotStepMinutes = 30,
                MaxCandidatePool = 30
            }));

        var request = new PlannerSuggestRequest
        {
            RoomId = requestedRoomId,
            StartTimeUtc = new DateTimeOffset(2026, 02, 20, 10, 0, 0, TimeSpan.Zero),
            EndTimeUtc = new DateTimeOffset(2026, 02, 20, 11, 0, 0, TimeSpan.Zero),
            Mode = mode,
            Priority = new PlannerPriorityDto { Time = 0.6, RoomProximity = 0.3, Building = 0.1 },
            Constraints = new PlannerConstraintsDto { SameDayOnly = true, MaxResults = 8, MaxTimeShiftMinutes = 180 }
        };

        return await service.SuggestAsync(request, Guid.NewGuid(), CancellationToken.None);
    }

    private async Task<SeedData> SeedBaseLayoutAsync()
    {
        await using var db = CreateDbContext();
        var createdBy = Guid.NewGuid();

        var buildingA = new Building
        {
            Id = Guid.NewGuid(),
            Name = "Science Building A",
            Code = "A",
            CreatedBy = createdBy
        };

        var buildingB = new Building
        {
            Id = Guid.NewGuid(),
            Name = "Science Building B",
            Code = "B",
            CreatedBy = createdBy
        };

        var requestedRoom = CreateRoom(buildingA.Id, "210", 2, createdBy);
        var sameFloorRoom = CreateRoom(buildingA.Id, "211", 2, createdBy);
        var otherFloorRoom = CreateRoom(buildingA.Id, "310", 3, createdBy);
        var otherBuildingRoom = CreateRoom(buildingB.Id, "110", 1, createdBy);

        db.Buildings.AddRange(buildingA, buildingB);
        db.Rooms.AddRange(requestedRoom, sameFloorRoom, otherFloorRoom, otherBuildingRoom);
        await db.SaveChangesAsync();

        return new SeedData
        {
            RequestedRoomId = requestedRoom.Id,
            SameFloorRoomId = sameFloorRoom.Id,
            OtherFloorRoomId = otherFloorRoom.Id,
            OtherBuildingRoomId = otherBuildingRoom.Id,
            StartUtc = new DateTimeOffset(2026, 02, 20, 10, 0, 0, TimeSpan.Zero),
            EndUtc = new DateTimeOffset(2026, 02, 20, 11, 0, 0, TimeSpan.Zero)
        };
    }

    private static Room CreateRoom(Guid buildingId, string number, int floor, Guid createdBy) => new()
    {
        Id = Guid.NewGuid(),
        BuildingId = buildingId,
        Number = number,
        Floor = floor,
        Capacity = 10,
        RoomTypeId = 1,
        IsActive = true,
        CreatedBy = createdBy
    };

    private async Task SeedConflictAsync(Guid roomId, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        await using var db = CreateDbContext();
        db.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            UserId = Guid.NewGuid(),
            StartTimeUtc = startUtc,
            EndTimeUtc = endUtc,
            Status = BookingStatus.Confirmed,
            Purpose = "conflict",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = Guid.NewGuid()
        });

        await db.SaveChangesAsync();
    }

    private async Task SeedAllDayConflictAsync(Guid roomId)
    {
        await using var db = CreateDbContext();
        db.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            UserId = Guid.NewGuid(),
            StartTimeUtc = new DateTimeOffset(2026, 02, 20, 8, 0, 0, TimeSpan.Zero),
            EndTimeUtc = new DateTimeOffset(2026, 02, 20, 20, 0, 0, TimeSpan.Zero),
            Status = BookingStatus.Confirmed,
            Purpose = "all-day-conflict",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = Guid.NewGuid()
        });

        await db.SaveChangesAsync();
    }

    private async Task ResetDatabaseAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakeRiskEvaluator : INoShowRiskEvaluator
    {
        public Task<BookingRiskEvaluationResult> EvaluateAsync(BookingRiskContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(new BookingRiskEvaluationResult
            {
                Probability = 0.2,
                ModelVersion = "test",
                FeaturesJson = "{}"
            });
        }
    }

    private sealed class FakeInvalidOllamaRanker : IOllamaRanker
    {
        public Task<IReadOnlyList<OllamaRankedItem>> RankAsync(OllamaRankingRequest request, CancellationToken cancellationToken)
        {
            IReadOnlyList<OllamaRankedItem> items = new[]
            {
                new OllamaRankedItem
                {
                    CandidateId = Guid.NewGuid().ToString(),
                    Reasons = new[] { "invalid id" }
                }
            };

            return Task.FromResult(items);
        }
    }

    private sealed class SeedData
    {
        public Guid RequestedRoomId { get; init; }
        public Guid SameFloorRoomId { get; init; }
        public Guid OtherFloorRoomId { get; init; }
        public Guid OtherBuildingRoomId { get; init; }
        public DateTimeOffset StartUtc { get; init; }
        public DateTimeOffset EndUtc { get; init; }
    }
}

[CollectionDefinition("AlternativeSlotSuggesterTests", DisableParallelization = true)]
public sealed class AlternativeSlotSuggesterCollection;
