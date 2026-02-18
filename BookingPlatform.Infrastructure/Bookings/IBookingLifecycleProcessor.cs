namespace BookingPlatform.Infrastructure.Bookings;

public interface IBookingLifecycleProcessor
{
    Task<BookingLifecycleProcessResult> ProcessAsync(CancellationToken cancellationToken);
}
