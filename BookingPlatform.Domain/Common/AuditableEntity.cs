namespace BookingPlatform.Domain.Common;

public abstract class AuditableEntity : Entity
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public Guid CreatedBy { get; set; }
}
