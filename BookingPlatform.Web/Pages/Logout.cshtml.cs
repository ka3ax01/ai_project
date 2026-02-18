using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookingPlatform.Web.Pages;

public class LogoutModel : PageModel
{
    public IActionResult OnGet()
    {
        Response.Cookies.Delete("UserAccessToken");
        Response.Cookies.Delete("AdminAccessToken");
        return RedirectToPage("/Login");
    }
}
