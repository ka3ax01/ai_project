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
    private readonly AppDbContext _dbContext;

    public CreateBuildingCommandHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BuildingDto> Handle(CreateBuildingCommand request, CancellationToken cancellationToken)
    {
        var building = new Building
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Code = request.Code
        };

        _dbContext.Buildings.Add(building);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
    private readonly AppDbContext _dbContext;

    public UpdateBuildingCommandHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BuildingDto> Handle(UpdateBuildingCommand request, CancellationToken cancellationToken)
    {
        var building = await _dbContext.Buildings.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (building is null)
        {
            throw new KeyNotFoundException("Building not found");
        }

        building.Name = request.Name;
        building.Code = request.Code;
        await _dbContext.SaveChangesAsync(cancellationToken);

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
    private readonly AppDbContext _dbContext;

    public DeleteBuildingCommandHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Unit> Handle(DeleteBuildingCommand request, CancellationToken cancellationToken)
    {
        var building = await _dbContext.Buildings.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);
        if (building is null)
        {
            return Unit.Value;
        }

        _dbContext.Buildings.Remove(building);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed class GetBuildingsQueryHandler : IRequestHandler<GetBuildingsQuery, IReadOnlyList<BuildingDto>>
{
    private readonly AppDbContext _dbContext;

    public GetBuildingsQueryHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<BuildingDto>> Handle(GetBuildingsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Buildings.AsNoTracking()
            .Select(b => new BuildingDto
            {
                Id = b.Id,
                Name = b.Name,
                Code = b.Code
            })
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetBuildingByIdQueryHandler : IRequestHandler<GetBuildingByIdQuery, BuildingDto?>
{
    private readonly AppDbContext _dbContext;

    public GetBuildingByIdQueryHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BuildingDto?> Handle(GetBuildingByIdQuery request, CancellationToken cancellationToken)
    {
        var building = await _dbContext.Buildings.AsNoTracking()
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

