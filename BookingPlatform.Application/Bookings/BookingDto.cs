namespace BookingPlatform.Application.Bookings;

public sealed class BookingDto
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset StartTimeUtc { get; set; }
    public DateTimeOffset EndTimeUtc { get; set; }
    public DateTimeOffset? ConfirmByUtc { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}
