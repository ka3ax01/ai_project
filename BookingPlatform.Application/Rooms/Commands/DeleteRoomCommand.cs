using MediatR;

namespace BookingPlatform.Application.Rooms.Commands;

public sealed record DeleteRoomCommand(Guid Id) : IRequest;

