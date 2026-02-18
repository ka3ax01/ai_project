using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookingPlatform.Application.Common;

namespace BookingPlatform.Web.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return null;
            }

            var user = httpContext.User;
            var idValue = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (Guid.TryParse(idValue, out var id))
            {
                return id;
            }

            if (TryResolveFromCookie(httpContext, "UserAccessToken", out id))
            {
                return id;
            }

            if (TryResolveFromCookie(httpContext, "AdminAccessToken", out id))
            {
                return id;
            }

            return null;
        }
    }

    private static bool TryResolveFromCookie(HttpContext httpContext, string cookieName, out Guid id)
    {
        id = default;

        if (!httpContext.Request.Cookies.TryGetValue(cookieName, out var token) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var jwt = handler.ReadJwtToken(token);
            var sub = jwt.Claims.FirstOrDefault(c =>
                c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(sub, out id);
        }
        catch
        {
            return false;
        }
    }
}
