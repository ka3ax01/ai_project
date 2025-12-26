using BookingPlatform.Application.Rooms;
using BookingPlatform.Application.Rooms.Commands;
using BookingPlatform.Application.Rooms.Queries;
using BookingPlatform.Domain.Enums;
using BookingPlatform.Domain.Rooms;
using BookingPlatform.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Infrastructure.Rooms;

public sealed class CreateRoomCommandHandler : IRequestHandler<CreateRoomCommand, RoomDto>
{
    private readonly AppDbContext _db;

    public CreateRoomCommandHandler(AppDbContext db) => _db = db;

    public async Task<RoomDto> Handle(CreateRoomCommand request, CancellationToken cancellationToken)
    {
        var room = new Room
        {
            BuildingId = request.BuildingId,
            Number = request.Number,
            Floor = request.Floor,
            Capacity = request.Capacity,
            RoomType = Enum.Parse<RoomType>(request.RoomType, ignoreCase: true),
            IsActive = request.IsActive
        };

        _db.Rooms.Add(room);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(room);
    }

    private static RoomDto ToDto(Room room) => new()
    {
        Id = room.Id,
        BuildingId = room.BuildingId,
        Number = room.Number,
        Floor = room.Floor,
        Capacity = room.Capacity,
        RoomType = room.RoomType.ToString(),
        IsActive = room.IsActive
    };
}

public sealed class UpdateRoomCommandHandler : IRequestHandler<UpdateRoomCommand, RoomDto>
{
    private readonly AppDbContext _db;

    public UpdateRoomCommandHandler(AppDbContext db) => _db = db;

    public async Task<RoomDto> Handle(UpdateRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (room is null)
        {
            throw new KeyNotFoundException("Room not found");
        }

        room.BuildingId = request.BuildingId;
        room.Number = request.Number;
        room.Floor = request.Floor;
        room.Capacity = request.Capacity;
        room.RoomType = Enum.Parse<RoomType>(request.RoomType, ignoreCase: true);
        room.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);

        return new RoomDto
        {
            Id = room.Id,
            BuildingId = room.BuildingId,
            Number = room.Number,
            Floor = room.Floor,
            Capacity = room.Capacity,
            RoomType = room.RoomType.ToString(),
            IsActive = room.IsActive
        };
    }
}

public sealed class DeleteRoomCommandHandler : IRequestHandler<DeleteRoomCommand>
{
    private readonly AppDbContext _db;

    public DeleteRoomCommandHandler(AppDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (room is null)
        {
            return Unit.Value;
        }

        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class GetRoomByIdQueryHandler : IRequestHandler<GetRoomByIdQuery, RoomDto?>
{
    private readonly AppDbContext _db;

    public GetRoomByIdQueryHandler(AppDbContext db) => _db = db;

    public async Task<RoomDto?> Handle(GetRoomByIdQuery request, CancellationToken cancellationToken)
    {
        var room = await _db.Rooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        return room is null
            ? null
            : new RoomDto
            {
                Id = room.Id,
                BuildingId = room.BuildingId,
                Number = room.Number,
                Floor = room.Floor,
                Capacity = room.Capacity,
                RoomType = room.RoomType.ToString(),
                IsActive = room.IsActive
            };
    }
}

public sealed class GetRoomsQueryHandler : IRequestHandler<GetRoomsQuery, IReadOnlyList<RoomDto>>
{
    private readonly AppDbContext _db;

    public GetRoomsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<RoomDto>> Handle(GetRoomsQuery request, CancellationToken cancellationToken)
    {
        return await _db.Rooms.AsNoTracking()
            .Select(r => new RoomDto
            {
                Id = r.Id,
                BuildingId = r.BuildingId,
                Number = r.Number,
                Floor = r.Floor,
                Capacity = r.Capacity,
                RoomType = r.RoomType.ToString(),
                IsActive = r.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}

