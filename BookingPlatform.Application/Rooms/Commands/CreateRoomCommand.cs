using MediatR;

namespace BookingPlatform.Application.Rooms.Commands;

public sealed record CreateRoomCommand(
    Guid BuildingId,
    string Number,
    int Floor,
    int Capacity,
    string RoomType,
    bool IsActive
) : IRequest<RoomDto>;

