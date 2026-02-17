namespace BookingPlatform.Domain.Dictionaries;

public sealed class UserRoleEntry
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
