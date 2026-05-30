using Bearcat.Abstractions.Hoster.Results;

namespace Bearcat.Abstractions.Hoster;

public interface IHosterWithCaptchaVerification : IHoster
{
    Task<CaptchaChallengeResult> RequestCaptchaChallengeAsync(
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    );

    Task<TryLoginResult> VerifyCaptchaAsync(
        IHosterConfig hosterConfig,
        string challenge,
        string response,
        CancellationToken cancellationToken
    );
}
