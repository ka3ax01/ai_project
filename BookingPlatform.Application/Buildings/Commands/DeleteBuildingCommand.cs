using MediatR;

namespace BookingPlatform.Application.Buildings.Commands;

public sealed record DeleteBuildingCommand(Guid Id) : IRequest;

