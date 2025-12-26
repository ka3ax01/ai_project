using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookingPlatform.Web.Pages.Admin;

public abstract class AdminPageModel : PageModel
{
    protected IActionResult RequireAdmin()
    {
        if (!Request.Cookies.TryGetValue("AdminAccessToken", out var token) || string.IsNullOrWhiteSpace(token))
        {
            return RedirectToPage("/Admin/Login");
        }

        var handler = new JwtSecurityTokenHandler();
        JwtSecurityToken? jwt;
        try
        {
            jwt = handler.ReadJwtToken(token);
        }
        catch
        {
            Response.Cookies.Delete("AdminAccessToken");
            return RedirectToPage("/Admin/Login");
        }

        var role = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Admin/Login");
        }

        return null!;
    }
}

