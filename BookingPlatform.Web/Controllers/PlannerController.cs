using BookingPlatform.Application.Planner;
using BookingPlatform.Application.Common;
using BookingPlatform.Infrastructure.Planner;
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
    private readonly IAlternativeSlotSuggester _alternativeSlotSuggester;
    private readonly IRequestContextAccessor _requestContextAccessor;

    public PlannerController(
        ISchedulePlanner schedulePlanner,
        IAlternativeSlotSuggester alternativeSlotSuggester,
        IRequestContextAccessor requestContextAccessor)
    {
        _schedulePlanner = schedulePlanner;
        _alternativeSlotSuggester = alternativeSlotSuggester;
        _requestContextAccessor = requestContextAccessor;
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

    [HttpPost("suggest")]
    [ProducesResponseType(typeof(PlannerSuggestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PlannerSuggestResponse>> Suggest(
        [FromBody] PlannerSuggestRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _requestContextAccessor.UserId
                     ?? throw new InvalidOperationException("Current user is not resolved.");

        var response = await _alternativeSlotSuggester.SuggestAsync(request, userId, cancellationToken);
        return Ok(response);
    }
}
