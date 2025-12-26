namespace BookingPlatform.Web.Pages.Admin;

using Microsoft.AspNetCore.Mvc;

public class IndexModel : AdminPageModel
{
    public IActionResult OnGet()
    {
        var result = RequireAdmin();
        if (result != null)
        {
            return result;
        }

        return Page();
    }
}
