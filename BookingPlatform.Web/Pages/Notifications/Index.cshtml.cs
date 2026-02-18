using BookingPlatform.Application.Common;
using BookingPlatform.Application.Notifications;
using BookingPlatform.Application.Notifications.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookingPlatform.Web.Pages.Notifications;

public sealed class IndexModel : PageModel
{
    private readonly IMediator _mediator;

    public IndexModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    public string Filter { get; private set; } = "all";
    public PagedResult<NotificationDto> Notifications { get; private set; } = new();
    public string InitialAccessToken { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync([FromQuery] string? filter, CancellationToken cancellationToken)
    {
        if (!TryGetAccessToken(out var token))
        {
            return RedirectToPage("/Login");
        }

        InitialAccessToken = token!;
        Filter = string.Equals(filter, "unread", StringComparison.OrdinalIgnoreCase) ? "unread" : "all";
        Notifications = await _mediator.Send(new GetMyNotificationsQuery(Filter, 50, 0), cancellationToken);
        return Page();
    }

    private bool TryGetAccessToken(out string? token)
    {
        if (Request.Cookies.TryGetValue("UserAccessToken", out token) && !string.IsNullOrWhiteSpace(token))
        {
            return true;
        }

        if (Request.Cookies.TryGetValue("AdminAccessToken", out token) && !string.IsNullOrWhiteSpace(token))
        {
            return true;
        }

        token = null;
        return false;
    }
}
