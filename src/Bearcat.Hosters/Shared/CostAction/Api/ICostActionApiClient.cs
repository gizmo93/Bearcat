using Bearcat.Hosters.Shared.CostAction.Api.Models;

namespace Bearcat.Hosters.Shared.CostAction.Api;

public interface ICostActionApiClient
{
    Task<CostActionUploadResult> UploadFileAsync(
        string apiKey,
        string appType,
        Stream stream,
        string fileName,
        long fileSize,
        string? folderId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, LinkCheckStatus>> CheckFilesAsync(
        string apiKey,
        string appType,
        IReadOnlyList<string> fileIds,
        CancellationToken cancellationToken
    );

    Task<string> CreateFolderAsync(
        string apiKey,
        string appType,
        string folderName,
        CancellationToken cancellationToken
    );

    Task MoveFileToFolderAsync(
        string apiKey,
        string fileId,
        string folderId,
        CancellationToken cancellationToken
    );

    Task VerifyAccessAsync(string apiKey, string appType, CancellationToken cancellationToken);
}
