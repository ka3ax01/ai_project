namespace BookingPlatform.Application.Common;

public interface IRequestContextAccessor
{
    Guid? UserId { get; }
    string CorrelationId { get; }
    string TraceId { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}
