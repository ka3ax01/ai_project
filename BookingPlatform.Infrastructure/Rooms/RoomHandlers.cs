using BookingPlatform.Application.Rooms;
using BookingPlatform.Application.Rooms.Commands;
using BookingPlatform.Application.Rooms.Queries;
using BookingPlatform.Domain.Enums;
using BookingPlatform.Domain.Rooms;
using BookingPlatform.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Infrastructure.Rooms;

internal static class RoomMappings
{
    public static RoomDto ToDto(Room room) => new()
    {
        Id = room.Id,
        BuildingId = room.BuildingId,
        Number = room.Number,
        Floor = room.Floor,
        Capacity = room.Capacity,
        RoomTypeId = room.RoomTypeId,
        RoomType = ((RoomType)room.RoomTypeId).ToString(),
        IsActive = room.IsActive
    };
}

public sealed class CreateRoomCommandHandler : IRequestHandler<CreateRoomCommand, RoomDto>
{
    private readonly AppDbContext _dbContext;

    public CreateRoomCommandHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RoomDto> Handle(CreateRoomCommand request, CancellationToken cancellationToken)
    {
        var roomTypeId = Enum.IsDefined(typeof(RoomType), request.RoomTypeId)
            ? request.RoomTypeId
            : (int)RoomType.Lecture;

        var room = new Room
        {
            Id = Guid.NewGuid(),
            BuildingId = request.BuildingId,
            Number = request.Number,
            Floor = request.Floor,
            Capacity = request.Capacity,
            RoomTypeId = roomTypeId,
            IsActive = request.IsActive
        };

        _dbContext.Rooms.Add(room);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return RoomMappings.ToDto(room);
    }
}

public sealed class UpdateRoomCommandHandler : IRequestHandler<UpdateRoomCommand, RoomDto>
{
    private readonly AppDbContext _dbContext;

    public UpdateRoomCommandHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RoomDto> Handle(UpdateRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (room is null)
        {
            throw new KeyNotFoundException("Room not found");
        }

        room.BuildingId = request.BuildingId;
        room.Number = request.Number;
        room.Floor = request.Floor;
        room.Capacity = request.Capacity;

        var roomTypeId = Enum.IsDefined(typeof(RoomType), request.RoomTypeId)
            ? request.RoomTypeId
            : room.RoomTypeId;

        room.RoomTypeId = roomTypeId;
        room.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return RoomMappings.ToDto(room);
    }
}

public sealed class DeleteRoomCommandHandler : IRequestHandler<DeleteRoomCommand>
{
    private readonly AppDbContext _dbContext;

    public DeleteRoomCommandHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Unit> Handle(DeleteRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (room is null)
        {
            return Unit.Value;
        }

        _dbContext.Rooms.Remove(room);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class GetRoomsQueryHandler : IRequestHandler<GetRoomsQuery, IReadOnlyList<RoomDto>>
{
    private readonly AppDbContext _dbContext;

    public GetRoomsQueryHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<RoomDto>> Handle(GetRoomsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Rooms.AsNoTracking()
            .Select(r => RoomMappings.ToDto(r))
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetRoomByIdQueryHandler : IRequestHandler<GetRoomByIdQuery, RoomDto?>
{
    private readonly AppDbContext _dbContext;

    public GetRoomByIdQueryHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RoomDto?> Handle(GetRoomByIdQuery request, CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        return room is null
            ? null
            : RoomMappings.ToDto(room);
    }
}
