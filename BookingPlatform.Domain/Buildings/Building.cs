using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Buildings;

public sealed class Building : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
