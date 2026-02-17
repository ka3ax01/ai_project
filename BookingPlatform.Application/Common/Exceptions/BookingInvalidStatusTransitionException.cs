namespace BookingPlatform.Application.Common.Exceptions;

public sealed class BookingInvalidStatusTransitionException : Exception
{
    public BookingInvalidStatusTransitionException(string message) : base(message)
    {
    }
}
