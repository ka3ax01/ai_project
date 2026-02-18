namespace BookingPlatform.Application.Bookings;

public interface INoShowRiskEvaluator
{
    Task<BookingRiskEvaluationResult> EvaluateAsync(BookingRiskContext context, CancellationToken cancellationToken);
}
