using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Web.Pages.Bookings;

public class MyModel : PageModel
{
    public string InitialAccessToken { get; private set; } = string.Empty;

    public IActionResult OnGet()
    {
        if (TryGetAccessToken(out var token))
        {
            InitialAccessToken = token!;
            return Page();
        }

        return RedirectToPage("/Login");
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
