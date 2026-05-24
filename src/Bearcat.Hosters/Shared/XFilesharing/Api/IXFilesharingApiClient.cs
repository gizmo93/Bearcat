namespace Bearcat.Hosters.Shared.XFilesharing.Api;

public interface IXFilesharingApiClient
{
    Task<Dictionary<string, bool>> FilesExistAsync(
        string apiKey,
        IReadOnlySet<string> fileCodes,
        CancellationToken cancellationToken
    );

    Task<AccountInfoResponse> GetAccountInfoAsync(
        string apiKey,
        CancellationToken cancellationToken
    );

    Task<RequestUploadResponse> RequestUploadAsync(
        string apiKey,
        CancellationToken cancellationToken
    );

    Task<UploadFileResponse> UploadFileAsync(
        Stream stream,
        string fileName,
        string uploadUrl,
        string sessionId,
        CancellationToken cancellationToken
    );
}
