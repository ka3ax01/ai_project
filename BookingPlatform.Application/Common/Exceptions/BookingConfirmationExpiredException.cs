namespace BookingPlatform.Application.Common.Exceptions;

public sealed class BookingConfirmationExpiredException : Exception
{
    public BookingConfirmationExpiredException(string message) : base(message)
    {
    }
}
