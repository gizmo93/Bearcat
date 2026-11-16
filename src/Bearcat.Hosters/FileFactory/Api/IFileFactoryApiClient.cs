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
}
