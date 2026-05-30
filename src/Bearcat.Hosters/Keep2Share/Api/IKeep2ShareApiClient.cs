namespace Bearcat.Hosters.Keep2Share.Api;

using Bearcat.Abstractions.Hoster.Results;

public interface IKeep2ShareApiClient
{
    Task<LoginResponse> LoginAsync(Keep2ShareConfig config, CancellationToken cancellationToken);

    Task<CaptchaChallengeResult> RequestCaptchaChallengeAsync(CancellationToken cancellationToken);

    Task<TryLoginResult> VerifyCaptchaAsync(
        Keep2ShareConfig config,
        string challenge,
        string response,
        CancellationToken cancellationToken
    );

    Task<AccountInfoResponse> GetAccountInfoAsync(
        Keep2ShareConfig config,
        CancellationToken cancellationToken
    );

    Task<UploadFormDataResponse> RequestUploadAsync(
        Keep2ShareConfig config,
        string? parentId,
        CancellationToken cancellationToken
    );

    Task<string> CreateFolderAsync(
        Keep2ShareConfig config,
        string folderName,
        CancellationToken cancellationToken
    );

    Task<UploadFileResponse> UploadFileAsync(
        UploadFormDataResponse uploadFormData,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        Keep2ShareConfig config,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    );
}
