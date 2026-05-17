using Bearcat.Hosters.DDownload.Api.AccountInfo;

namespace Bearcat.Hosters.DDownload.Api;

public interface IDDownloadApiClient
{
    Task<Dictionary<string, bool>> FilesExistAsync(
        string apiKey,
        IReadOnlySet<string> fileCodes,
        CancellationToken cancellationToken
    );

    Task<Response> GetAccountInfoAsync(string apiKey, CancellationToken cancellationToken);

    Task<RequestUpload.Response> RequestUploadAsync(
        string apiKey,
        CancellationToken cancellationToken
    );

    Task<UploadFile.Response> UploadFileAsync(
        Stream stream,
        string fileName,
        string uploadUrl,
        string sessionId,
        CancellationToken cancellationToken
    );
}
