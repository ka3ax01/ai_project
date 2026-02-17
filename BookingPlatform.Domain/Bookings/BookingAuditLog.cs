using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Bookings;

public sealed class BookingAuditLog : Entity
{
    public Guid BookingId { get; set; }
    public string Action { get; set; } = string.Empty;
    public int? OldStatus { get; set; }
    public int? NewStatus { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
