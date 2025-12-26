using BookingPlatform.Domain.Common;
using BookingPlatform.Domain.Enums;

namespace BookingPlatform.Domain.Bookings;

public sealed class Booking : AuditableEntity
{
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset StartTimeUtc { get; set; }
    public DateTimeOffset EndTimeUtc { get; set; }
    public BookingStatus Status { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
