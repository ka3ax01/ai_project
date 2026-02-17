using BookingPlatform.Application.Rooms;
using MediatR;

namespace BookingPlatform.Application.Rooms.Commands;

public sealed record CreateRoomCommand(
    Guid BuildingId,
    string Number,
    int Floor,
    int Capacity,
    int RoomTypeId,
    bool IsActive
) : IRequest<RoomDto>;
