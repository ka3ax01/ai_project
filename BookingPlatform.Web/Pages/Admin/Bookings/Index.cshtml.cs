using BookingPlatform.Application.Bookings;
using BookingPlatform.Application.Bookings.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Web.Pages.Admin.Bookings;

public class IndexModel : AdminPageModel
{
    private readonly IMediator _mediator;

    public IndexModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    public IReadOnlyList<BookingDto> Bookings { get; private set; } = Array.Empty<BookingDto>();

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
    {
        var redirect = RequireAdmin();
        if (redirect != null)
        {
            return redirect;
        }

        Bookings = await _mediator.Send(new GetBookingsQuery(), cancellationToken);
        return Page();
    }
}

