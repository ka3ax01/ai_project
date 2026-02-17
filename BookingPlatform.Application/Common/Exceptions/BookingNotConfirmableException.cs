namespace BookingPlatform.Application.Common.Exceptions;

public sealed class BookingNotConfirmableException : Exception
{
    public BookingNotConfirmableException(string message) : base(message)
    {
    }
}
