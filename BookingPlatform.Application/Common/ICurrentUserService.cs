namespace BookingPlatform.Application.Common;

public interface ICurrentUserService
{
    Guid? UserId { get; }
}

