namespace BookingPlatform.Application.Common.Exceptions;

public sealed class BookingConflictException : Exception
{
    public BookingConflictException(string message) : base(message)
    {
    }
}
