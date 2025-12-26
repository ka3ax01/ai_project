using BookingPlatform.Application.Buildings;
using MediatR;

namespace BookingPlatform.Application.Buildings.Commands;

public sealed record CreateBuildingCommand(
    string Name,
    string Code
) : IRequest<BuildingDto>;

