using BookingPlatform.Application.Common;
using BookingPlatform.Application.Notifications.Commands;
using BookingPlatform.Application.Notifications;
using BookingPlatform.Application.Notifications.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(PagedResult<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<NotificationDto>>> GetMy(
        [FromQuery] string status = "all",
        [FromQuery] int take = 20,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "unread", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem(detail: "status must be 'all' or 'unread'.");
        }

        if (take <= 0 || take > 100)
        {
            return ValidationProblem(detail: "take must be between 1 and 100.");
        }

        if (skip < 0)
        {
            return ValidationProblem(detail: "skip must be greater than or equal to 0.");
        }

        var result = await _mediator.Send(new GetMyNotificationsQuery(status, take, skip), cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<PagedResult<NotificationDto>>> GetMyLegacy(
        [FromQuery] string status = "all",
        [FromQuery] int take = 20,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default) =>
        GetMy(status, take, skip, cancellationToken);

    [HttpGet("my/unread-count")]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var count = await _mediator.Send(new GetUnreadCountQuery(), cancellationToken);
        return Ok(new UnreadCountResponse { Count = count });
    }

    [HttpPost("my/mark-read")]
    [ProducesResponseType(typeof(MarkUpdatedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MarkUpdatedResponse>> MarkRead(
        [FromBody] MarkReadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Ids is null || request.Ids.Count == 0)
        {
            return ValidationProblem(detail: "ids must not be empty.");
        }

        var updated = await _mediator.Send(new MarkNotificationsReadCommand(request.Ids), cancellationToken);
        return Ok(new MarkUpdatedResponse { Updated = updated });
    }

    [HttpPost("my/mark-all-read")]
    [ProducesResponseType(typeof(MarkUpdatedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MarkUpdatedResponse>> MarkAllRead(CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(new MarkAllNotificationsReadCommand(), cancellationToken);
        return Ok(new MarkUpdatedResponse { Updated = updated });
    }
}

public sealed class MarkReadRequest
{
    public IReadOnlyList<Guid> Ids { get; set; } = Array.Empty<Guid>();
}

public sealed class UnreadCountResponse
{
    public int Count { get; set; }
}

public sealed class MarkUpdatedResponse
{
    public int Updated { get; set; }
}
