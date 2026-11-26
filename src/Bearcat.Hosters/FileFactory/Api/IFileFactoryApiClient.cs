using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;

namespace Bearcat.Hosters.FileFactory.Api;

public interface IFileFactoryApiClient
{
    Task<FileFactoryUploadResult> UploadFileAsync(
        FileFactoryConfig config,
        Stream stream,
        string fileName,
        long fileSize,
        string? folderId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, HostedFileInfo>> GetFileInfoPerFileIdAsync(
        FileFactoryConfig config,
        IReadOnlyList<string> fileIds,
        CancellationToken cancellationToken
    );

    Task EnsureCredentialsAcceptedAsync(
        FileFactoryConfig config,
        CancellationToken cancellationToken
    );

    Task<string> CreateFolderAsync(
        FileFactoryConfig config,
        string folderName,
        CancellationToken cancellationToken
    );

    Task MoveFilesToFolderAsync(
        FileFactoryConfig config,
        IReadOnlyList<string> fileIds,
        string folderId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<HosterFolderContentStateDto>> GetFolderContentStatesAsync(
        FileFactoryConfig config,
        IReadOnlyList<string> folderIds,
        CancellationToken cancellationToken
    );

    Task DeleteFolderAsync(
        FileFactoryConfig config,
        string folderId,
        CancellationToken cancellationToken
    );
}
