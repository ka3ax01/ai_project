using BookingPlatform.Application.Rooms;
using BookingPlatform.Application.Rooms.Commands;
using BookingPlatform.Application.Rooms.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IMediator _mediator;

    public RoomsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoomDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRoomsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoomDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRoomByIdQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<RoomDto>> Create([FromBody] CreateRoomCommand command, CancellationToken cancellationToken)
    {
        var created = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoomDto>> Update(Guid id, [FromBody] UpdateRoomCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest("Id in route and command mismatch");
        }

        var updated = await _mediator.Send(command, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteRoomCommand(id), cancellationToken);
        return NoContent();
    }
}

