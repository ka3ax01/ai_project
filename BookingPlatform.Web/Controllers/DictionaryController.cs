using BookingPlatform.Domain.Dictionaries;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[ApiExplorerSettings(GroupName = "v1")]
public class DictionaryController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public DictionaryController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("user-roles")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<UserRoleEntry>>> GetUserRoles(CancellationToken cancellationToken)
    {
        var roles = await _dbContext.UserRoles.AsNoTracking().ToListAsync(cancellationToken);
        return Ok(roles);
    }

    [HttpGet("room-types")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<RoomTypeEntry>>> GetRoomTypes(CancellationToken cancellationToken)
    {
        var types = await _dbContext.RoomTypes.AsNoTracking().ToListAsync(cancellationToken);
        return Ok(types);
    }
}
