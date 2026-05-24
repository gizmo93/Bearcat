namespace Bearcat.Hosters.KrakenFiles.Api;

public interface IKrakenFilesApiClient
{
    Task<UploadFileResponse> UploadFileAsync(
        KrakenFilesConfig config,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        KrakenFilesConfig config,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    );

    Task<bool> IsApiKeyValidAsync(KrakenFilesConfig config, CancellationToken cancellationToken);
}
