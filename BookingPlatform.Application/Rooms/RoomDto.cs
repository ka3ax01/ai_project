namespace BookingPlatform.Application.Rooms;

public sealed class RoomDto
{
    public Guid Id { get; set; }
    public Guid BuildingId { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Floor { get; set; }
    public int Capacity { get; set; }
    public int RoomTypeId { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

