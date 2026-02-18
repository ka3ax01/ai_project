using BookingPlatform.Application.Planner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Web.Controllers;

[ApiController]
[Route("api/planner")]
[Authorize]
[Produces("application/json")]
public class PlannerController : ControllerBase
{
    private readonly ISchedulePlanner _schedulePlanner;

    public PlannerController(ISchedulePlanner schedulePlanner)
    {
        _schedulePlanner = schedulePlanner;
    }

    [HttpPost("propose")]
    [ProducesResponseType(typeof(ScheduleProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ScheduleProposalDto>> Propose(
        [FromBody] PlannerRequest request,
        CancellationToken cancellationToken)
    {
        var proposal = await _schedulePlanner.ProposeAsync(request, cancellationToken);
        return Ok(proposal);
    }
}
