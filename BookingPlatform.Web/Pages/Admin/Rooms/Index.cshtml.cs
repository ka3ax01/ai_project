using BookingPlatform.Application.Rooms;
using BookingPlatform.Application.Rooms.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Web.Pages.Admin.Rooms;

public class IndexModel : AdminPageModel
{
    private readonly IMediator _mediator;

    public IndexModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    public IReadOnlyList<RoomDto> Rooms { get; private set; } = Array.Empty<RoomDto>();

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
    {
        var redirect = RequireAdmin();
        if (redirect != null)
        {
            return redirect;
        }

        Rooms = await _mediator.Send(new GetRoomsQuery(), cancellationToken);
        return Page();
    }
}

