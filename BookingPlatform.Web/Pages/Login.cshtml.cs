using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookingPlatform.Application.Auth;
using BookingPlatform.Application.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookingPlatform.Web.Pages;

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

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string ErrorMessage { get; private set; } = string.Empty;

    public IActionResult OnGet()
    {
        if (Request.Cookies.ContainsKey("UserAccessToken") || Request.Cookies.ContainsKey("AdminAccessToken"))
        {
            return RedirectToPage("/Index");
        }

        return Page();
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
            ErrorMessage = "Invalid username or password.";
            return Page();
        }

        var handler = new JwtSecurityTokenHandler();
        JwtSecurityToken jwt;
        try
        {
            jwt = handler.ReadJwtToken(authResult.AccessToken);
        }
        catch
        {
            ErrorMessage = "Failed to parse access token.";
            return Page();
        }

        var role = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

        Response.Cookies.Append("UserAccessToken", authResult.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(12)
        });

        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            Response.Cookies.Append("AdminAccessToken", authResult.AccessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(12)
            });
        }
        else
        {
            Response.Cookies.Delete("AdminAccessToken");
        }

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return RedirectToPage("/Index");
    }
}
