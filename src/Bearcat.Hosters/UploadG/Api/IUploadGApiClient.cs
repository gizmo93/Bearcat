using Bearcat.Abstractions.Hoster.Dto;

namespace Bearcat.Hosters.UploadG.Api;

public interface IUploadGApiClient
{
    Task<UploadFileResponse> UploadFileAsync(
        UploadGConfig config,
        Stream stream,
        string fileName,
        string? folderId,
        long fileSize,
        CancellationToken cancellationToken
    );

    Task<string> CreateFolderAsync(
        UploadGConfig config,
        string folderName,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, bool>> CheckLinksAsync(
        UploadGConfig config,
        IReadOnlyList<FileUrlToCheckDto> files,
        CancellationToken cancellationToken
    );

    Task<bool> IsApiKeyValidAsync(UploadGConfig config, CancellationToken cancellationToken);

    Task<string> GetOrCreateShareableLinkAsync(
        UploadGConfig config,
        long entryId,
        CancellationToken cancellationToken
    );
}
