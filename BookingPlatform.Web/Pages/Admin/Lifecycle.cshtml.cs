using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Web.Pages.Admin;

public class LifecycleModel : AdminPageModel
{
    public string AccessToken { get; private set; } = string.Empty;

    public IActionResult OnGet()
    {
        var redirect = RequireAdmin();
        if (redirect != null)
        {
            return redirect;
        }

        if (Request.Cookies.TryGetValue("AdminAccessToken", out var token) && !string.IsNullOrWhiteSpace(token))
        {
            AccessToken = token;
        }

        return Page();
    }
}
