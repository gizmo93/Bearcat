namespace Bearcat.Abstractions.Hoster.Exceptions;

public class HosterFileNotFoundException(string message, Exception? innerException = null)
    : Exception(message, innerException);
