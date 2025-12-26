using MediatR;

namespace BookingPlatform.Application.Rooms.Queries;

public sealed record GetRoomsQuery : IRequest<IReadOnlyList<RoomDto>>;

