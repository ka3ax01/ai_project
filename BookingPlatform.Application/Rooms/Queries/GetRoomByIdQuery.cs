using BookingPlatform.Application.Rooms;
using MediatR;

namespace BookingPlatform.Application.Rooms.Queries;

public sealed record GetRoomByIdQuery(Guid Id) : IRequest<RoomDto?>;

