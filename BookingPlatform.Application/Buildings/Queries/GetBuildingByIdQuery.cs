using MediatR;

namespace BookingPlatform.Application.Buildings.Queries;

public sealed record GetBuildingByIdQuery(Guid Id) : IRequest<BuildingDto?>;

