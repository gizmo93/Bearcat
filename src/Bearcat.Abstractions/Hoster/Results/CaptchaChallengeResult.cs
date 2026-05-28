namespace Bearcat.Abstractions.Hoster.Results;

public record CaptchaChallengeResult(
    bool IsSuccess,
    string? Challenge = null,
    string? CaptchaUrl = null,
    string? ErrorMessage = null
);
