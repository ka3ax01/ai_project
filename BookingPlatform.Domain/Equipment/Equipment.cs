using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Equipment;

public sealed class Equipment : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class RoomEquipment : AuditableEntity
{
    public Guid RoomId { get; set; }
    public Guid EquipmentId { get; set; }
    public int Quantity { get; set; }
}
