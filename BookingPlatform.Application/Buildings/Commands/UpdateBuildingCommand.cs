using BookingPlatform.Application.Buildings;
using MediatR;

namespace BookingPlatform.Application.Buildings.Commands;

public sealed record UpdateBuildingCommand(
    Guid Id,
    string Name,
    string Code
) : IRequest<BuildingDto>;

