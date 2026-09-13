using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Shared;

namespace Bearcat.Hosters.Fast2Share.Api;

public interface IFast2ShareApiClient
{
    Task<Fast2ShareUploadResult> UploadFileAsync(
        Fast2ShareConfig config,
        string fullFileName,
        Stream stream,
        string fileName,
        long fileSize,
        string? folderId,
        CancellationToken cancellationToken
    );

    Task<string> CreateFolderAsync(
        Fast2ShareConfig config,
        string folderName,
        CancellationToken cancellationToken
    );

    Task MoveFileToFolderAsync(
        Fast2ShareConfig config,
        string fileUrl,
        string? externalId,
        string folderId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, LinkCheckStatus>> CheckLinksAsync(
        Fast2ShareConfig config,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    );

    Task<bool> IsApiKeyValidAsync(Fast2ShareConfig config, CancellationToken cancellationToken);
}

public record Fast2ShareUploadResult(string Uuid, string FileUrl, bool Deduped);
