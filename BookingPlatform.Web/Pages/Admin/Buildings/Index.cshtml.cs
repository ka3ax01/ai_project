using BookingPlatform.Application.Buildings;
using BookingPlatform.Application.Buildings.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Web.Pages.Admin.Buildings;

public class IndexModel : AdminPageModel
{
    private readonly IMediator _mediator;

    public IndexModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    public IReadOnlyList<BuildingDto> Buildings { get; private set; } = Array.Empty<BuildingDto>();

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
    {
        var redirect = RequireAdmin();
        if (redirect != null)
        {
            return redirect;
        }

        Buildings = await _mediator.Send(new GetBuildingsQuery(), cancellationToken);
        return Page();
    }
}
