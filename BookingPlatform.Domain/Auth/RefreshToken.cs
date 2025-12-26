using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Auth;

public sealed class RefreshToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool IsRevoked { get; set; }
}

