using System.ComponentModel.DataAnnotations;
using BookingPlatform.Application.Buildings.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Web.Pages.Admin.Buildings;

public class CreateModel : AdminPageModel
{
    private readonly IMediator _mediator;

    public CreateModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    [BindProperty]
    [Required]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    public string Code { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        var redirect = RequireAdmin();
        return redirect ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var redirect = RequireAdmin();
        if (redirect != null)
        {
            return redirect;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await _mediator.Send(new CreateBuildingCommand(Name, Code), cancellationToken);
        return RedirectToPage("Index");
    }
}
