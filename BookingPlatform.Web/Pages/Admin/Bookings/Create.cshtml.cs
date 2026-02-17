using System.ComponentModel.DataAnnotations;
using BookingPlatform.Application.Bookings.Commands;
using BookingPlatform.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Web.Pages.Admin.Bookings;

public class CreateModel : AdminPageModel
{
    private readonly IMediator _mediator;
    private readonly AppDbContext _dbContext;

    public CreateModel(IMediator mediator, AppDbContext dbContext)
    {
        _mediator = mediator;
        _dbContext = dbContext;
    }

    public IReadOnlyList<(Guid Id, string Label)> Rooms { get; private set; } = Array.Empty<(Guid, string)>();

    [BindProperty]
    [Required]
    public Guid RoomId { get; set; }

    [BindProperty]
    [Required]
    public DateTimeOffset StartTimeUtc { get; set; }

    [BindProperty]
    [Required]
    public DateTimeOffset EndTimeUtc { get; set; }

    [BindProperty]
    public string Purpose { get; set; } = string.Empty;

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        Rooms = await _dbContext.Rooms
            .AsNoTracking()
            .Select(r => new ValueTuple<Guid, string>(r.Id, r.Number))
            .ToListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
    {
        var redirect = RequireAdmin();
        if (redirect != null)
        {
            return redirect;
        }

        await LoadLookupsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var redirect = RequireAdmin();
        if (redirect != null)
        {
            return redirect;
        }

        await LoadLookupsAsync(cancellationToken);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var command = new CreateBookingCommand(RoomId, StartTimeUtc, EndTimeUtc, Purpose);
        await _mediator.Send(command, cancellationToken);

        return RedirectToPage("Index");
    }
}

