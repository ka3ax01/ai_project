using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookingPlatform.Application.Common;

namespace BookingPlatform.Web.Services;

public sealed class RequestContextAccessor : IRequestContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context is null)
            {
                return null;
            }

            var idValue = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (Guid.TryParse(idValue, out var userId))
            {
                return userId;
            }

            if (TryResolveFromCookie(context, "UserAccessToken", out userId))
            {
                return userId;
            }

            if (TryResolveFromCookie(context, "AdminAccessToken", out userId))
            {
                return userId;
            }

            return null;
        }
    }

    public string CorrelationId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context is null)
            {
                return string.Empty;
            }

            var headerValue = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(headerValue) ? context.TraceIdentifier : headerValue;
        }
    }

    public string TraceId => _httpContextAccessor.HttpContext?.TraceIdentifier ?? string.Empty;

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

    private static bool TryResolveFromCookie(HttpContext context, string cookieName, out Guid userId)
    {
        userId = default;

        if (!context.Request.Cookies.TryGetValue(cookieName, out var token) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var jwt = handler.ReadJwtToken(token);
            var sub = jwt.Claims.FirstOrDefault(c =>
                c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(sub, out userId);
        }
        catch
        {
            return false;
        }
    }
}
