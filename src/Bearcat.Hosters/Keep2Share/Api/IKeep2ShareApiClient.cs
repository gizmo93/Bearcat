namespace Bearcat.Hosters.Keep2Share.Api;

public interface IKeep2ShareApiClient
{
    Task<LoginResponse> LoginAsync(Keep2ShareConfig config, CancellationToken cancellationToken);

    Task<AccountInfoResponse> GetAccountInfoAsync(
        Keep2ShareConfig config,
        CancellationToken cancellationToken
    );

    Task<UploadFormDataResponse> RequestUploadAsync(
        Keep2ShareConfig config,
        CancellationToken cancellationToken
    );

    Task<UploadFileResponse> UploadFileAsync(
        UploadFormDataResponse uploadFormData,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    );
}
