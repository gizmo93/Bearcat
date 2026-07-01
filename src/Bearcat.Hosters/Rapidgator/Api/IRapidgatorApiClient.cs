using Bearcat.Hosters.Rapidgator.Api.File;

namespace Bearcat.Hosters.Rapidgator.Api;

public interface IRapidgatorApiClient
{
    Task<UploadFileResponse> RequestUploadFileAsync(
        string name,
        long size,
        string hash,
        string? folderId,
        RapidgatorConfig config,
        CancellationToken cancellationToken
    );

    Task<UploadFileResponse> ChangeFileModeAsync(
        RapidgatorConfig config,
        string fileId,
        UploadMode mode,
        CancellationToken cancellationToken
    );

    Task<UploadFileResponse> UploadFileAsync(
        string uploadUrl,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken
    );

    Task<UploadFileResponse> GetUploadInfoAsync(
        RapidgatorConfig config,
        string uploadId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        RapidgatorConfig config,
        IReadOnlyList<string> links,
        CancellationToken cancellationToken
    );

    Task<string> CreateFolderAsync(
        string folderName,
        RapidgatorConfig config,
        CancellationToken cancellationToken
    );

    Task MoveFileToFolderAsync(
        RapidgatorConfig config,
        string fileUrl,
        string folderId,
        CancellationToken cancellationToken
    );
}
