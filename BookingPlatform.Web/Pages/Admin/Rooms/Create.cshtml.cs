using System.ComponentModel.DataAnnotations;
using BookingPlatform.Application.Rooms.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Web.Pages.Admin.Rooms;

public class CreateModel : AdminPageModel
{
    private readonly IMediator _mediator;

    public CreateModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    [BindProperty]
    [Required]
    public Guid BuildingId { get; set; }

    [BindProperty]
    [Required]
    public string Number { get; set; } = string.Empty;

    [BindProperty]
    public int Floor { get; set; }

    [BindProperty]
    public int Capacity { get; set; }

    [BindProperty]
    [Required]
    public string RoomType { get; set; } = "Lecture";

    [BindProperty]
    public bool IsActive { get; set; } = true;

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

        await _mediator.Send(new CreateRoomCommand(BuildingId, Number, Floor, Capacity, RoomType, IsActive), cancellationToken);
        return RedirectToPage("Index");
    }
}

