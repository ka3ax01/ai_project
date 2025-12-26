using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookingPlatform.Application.Auth;
using BookingPlatform.Application.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookingPlatform.Web.Pages.Admin;

public class LoginModel : PageModel
{
    private readonly IMediator _mediator;

    public LoginModel(IMediator mediator)
    {
        _mediator = mediator;
    }

    [BindProperty]
    [Required]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    public string Password { get; set; } = string.Empty;

    public string ErrorMessage { get; private set; } = string.Empty;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        AuthResultDto authResult;
        try
        {
            authResult = await _mediator.Send(new LoginCommand(Username, Password), cancellationToken);
        }
        catch
        {
            ErrorMessage = "Неверный логин или пароль.";
            return Page();
        }

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(authResult.AccessToken);
        var role = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "У вас нет прав администратора.";
            return Page();
        }

        Response.Cookies.Append("AdminAccessToken", authResult.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax
        });

        return RedirectToPage("/Admin/Index");
    }
}

