using BookingPlatform.Application.Buildings;
using BookingPlatform.Application.Buildings.Commands;
using BookingPlatform.Application.Buildings.Queries;
using BookingPlatform.Domain.Buildings;
using BookingPlatform.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Infrastructure.Buildings;

public sealed class CreateBuildingCommandHandler : IRequestHandler<CreateBuildingCommand, BuildingDto>
{
    private readonly AppDbContext _db;

    public CreateBuildingCommandHandler(AppDbContext db) => _db = db;

    public async Task<BuildingDto> Handle(CreateBuildingCommand request, CancellationToken cancellationToken)
    {
        var building = new Building
        {
            Name = request.Name,
            Code = request.Code,
        };

        _db.Buildings.Add(building);
        await _db.SaveChangesAsync(cancellationToken);

        return new BuildingDto
        {
            Id = building.Id,
            Name = building.Name,
            Code = building.Code
        };
    }
}

public sealed class UpdateBuildingCommandHandler : IRequestHandler<UpdateBuildingCommand, BuildingDto>
{
    private readonly AppDbContext _db;

    public UpdateBuildingCommandHandler(AppDbContext db) => _db = db;

    public async Task<BuildingDto> Handle(UpdateBuildingCommand request, CancellationToken cancellationToken)
    {
        var building = await _db.Buildings.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (building is null)
        {
            throw new KeyNotFoundException("Building not found");
        }

        building.Name = request.Name;
        building.Code = request.Code;

        await _db.SaveChangesAsync(cancellationToken);

        return new BuildingDto
        {
            Id = building.Id,
            Name = building.Name,
            Code = building.Code
        };
    }
}

public sealed class DeleteBuildingCommandHandler : IRequestHandler<DeleteBuildingCommand>
{
    private readonly AppDbContext _db;

    public DeleteBuildingCommandHandler(AppDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteBuildingCommand request, CancellationToken cancellationToken)
    {
        var building = await _db.Buildings.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (building is null)
        {
            return Unit.Value;
        }

        _db.Buildings.Remove(building);
        await _db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class GetBuildingByIdQueryHandler : IRequestHandler<GetBuildingByIdQuery, BuildingDto?>
{
    private readonly AppDbContext _db;

    public GetBuildingByIdQueryHandler(AppDbContext db) => _db = db;

    public async Task<BuildingDto?> Handle(GetBuildingByIdQuery request, CancellationToken cancellationToken)
    {
        var building = await _db.Buildings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        return building is null
            ? null
            : new BuildingDto
            {
                Id = building.Id,
                Name = building.Name,
                Code = building.Code
            };
    }
}

public sealed class GetBuildingsQueryHandler : IRequestHandler<GetBuildingsQuery, IReadOnlyList<BuildingDto>>
{
    private readonly AppDbContext _db;

    public GetBuildingsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<BuildingDto>> Handle(GetBuildingsQuery request, CancellationToken cancellationToken)
    {
        return await _db.Buildings.AsNoTracking()
            .Select(b => new BuildingDto
            {
                Id = b.Id,
                Name = b.Name,
                Code = b.Code
            })
            .ToListAsync(cancellationToken);
    }
}

