using BookingPlatform.Domain.Common;
using BookingPlatform.Domain.Enums;

namespace BookingPlatform.Domain.Users;

public sealed class User : AuditableEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int RoleId { get; set; }
}

public static class UserConstants
{
    public static readonly Guid SystemUserId = new("6f9e2b1a-4c3d-4e5f-8a9b-0c1d2e3f4a5b");
}
