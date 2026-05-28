using Refit;

namespace Bearcat.Hosters.Keep2Share.Api;

public interface IKeep2ShareApi
{
    [Post("/login")]
    Task<LoginResponse> LoginAsync(
        [Body] LoginRequest request,
        CancellationToken cancellationToken
    );

    [Post("/requestReCaptcha")]
    Task<CaptchaChallengeResponse> RequestReCaptchaAsync(CancellationToken cancellationToken);

    [Post("/accountInfo")]
    Task<AccountInfoResponse> GetAccountInfoAsync(
        [Body] AuthenticatedRequest request,
        CancellationToken cancellationToken
    );

    [Post("/getUploadFormData")]
    Task<UploadFormDataResponse> GetUploadFormDataAsync(
        [Body] UploadFormDataRequest request,
        CancellationToken cancellationToken
    );

    [Post("/getFileStatus")]
    Task<FileStatusResponse> GetFileStatusAsync(
        [Body] FileStatusRequest request,
        CancellationToken cancellationToken
    );

    [Post("/getFilesInfo")]
    Task<GetFilesInfoResponse> GetFilesInfoAsync(
        [Body] GetFilesInfoRequest request,
        CancellationToken cancellationToken
    );
}
