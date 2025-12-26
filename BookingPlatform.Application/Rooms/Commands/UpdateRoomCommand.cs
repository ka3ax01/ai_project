using BookingPlatform.Application.Rooms;
using MediatR;

namespace BookingPlatform.Application.Rooms.Commands;

public sealed record UpdateRoomCommand(
    Guid Id,
    Guid BuildingId,
    string Number,
    int Floor,
    int Capacity,
    string RoomType,
    bool IsActive
) : IRequest<RoomDto>;

