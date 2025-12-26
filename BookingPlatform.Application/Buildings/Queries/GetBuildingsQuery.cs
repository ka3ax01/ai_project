using MediatR;

namespace BookingPlatform.Application.Buildings.Queries;

public sealed record GetBuildingsQuery : IRequest<IReadOnlyList<BuildingDto>>;

