using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookingPlatform.Application.Bookings;
using BookingPlatform.Application.Bookings.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IMediator _mediator;

    public IndexModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    public IReadOnlyList<BookingDto> MyBookings { get; private set; } = Array.Empty<BookingDto>();
    public bool IsAdmin { get; private set; }
    public string CurrentUsername { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
    {
        var token = ResolveAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return RedirectToPage("/Login");
        }

        var handler = new JwtSecurityTokenHandler();
        JwtSecurityToken jwt;
        try
        {
            jwt = handler.ReadJwtToken(token);
        }
        catch
        {
            Response.Cookies.Delete("UserAccessToken");
            Response.Cookies.Delete("AdminAccessToken");
            return RedirectToPage("/Login");
        }

        var sub = jwt.Claims.FirstOrDefault(c =>
            c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value;
        var role = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? string.Empty;
        var username = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty;

        IsAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        CurrentUsername = username;

        if (Guid.TryParse(sub, out var userId))
        {
            var allBookings = await _mediator.Send(new GetBookingsQuery(), cancellationToken);
            MyBookings = allBookings
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.StartTimeUtc)
                .ToList();
        }

        return Page();
    }

    private string? ResolveAccessToken()
    {
        if (Request.Cookies.TryGetValue("UserAccessToken", out var userToken) &&
            !string.IsNullOrWhiteSpace(userToken))
        {
            return userToken;
        }

        if (Request.Cookies.TryGetValue("AdminAccessToken", out var adminToken) &&
            !string.IsNullOrWhiteSpace(adminToken))
        {
            return adminToken;
        }

        return null;
    }
}
