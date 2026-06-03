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

    Task<string> CreateFolderAsync(
        string apiKey,
        string folderName,
        CancellationToken cancellationToken
    );

    Task SetFileFolderAsync(
        string apiKey,
        string fileCode,
        string folderId,
        CancellationToken cancellationToken
    );

    Task SetFilePropertiesAsync(
        string apiKey,
        string fileCode,
        bool premiumOnly,
        CancellationToken cancellationToken
    );
}
