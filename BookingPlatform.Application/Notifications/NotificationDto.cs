namespace BookingPlatform.Application.Notifications;

public sealed class NotificationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
}
