using BookingPlatform.Domain.Common;
using BookingPlatform.Domain.Enums;

namespace BookingPlatform.Domain.Rooms;

public sealed class Room : AuditableEntity
{
    public Guid BuildingId { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Floor { get; set; }
    public int Capacity { get; set; }
    public int RoomTypeId { get; set; }
    public bool IsActive { get; set; }
}
