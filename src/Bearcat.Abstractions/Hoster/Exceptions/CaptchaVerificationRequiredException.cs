namespace Bearcat.Abstractions.Hoster.Exceptions;

public class CaptchaVerificationRequiredException(
    string message,
    int? code = null,
    int? errorCode = null
) : Exception(message)
{
    public int? Code { get; } = code;

    public int? ErrorCode { get; } = errorCode;
}
