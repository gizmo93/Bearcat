using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;

namespace Bearcat.Hosters.Filer.Api;

public interface IFilerApiClient
{
    Task<FilerUploadResult> UploadFileAsync(
        FilerConfig config,
        Stream stream,
        string fileName,
        long fileSize,
        string? folderHash,
        CancellationToken cancellationToken
    );

    Task<string> CreateFolderAsync(
        FilerConfig config,
        string folderName,
        CancellationToken cancellationToken
    );

    Task MoveFileToFolderAsync(
        FilerConfig config,
        string fileHash,
        string editCode,
        string folderHash,
        CancellationToken cancellationToken
    );

    Task MoveFilesToFolderAsync(
        FilerConfig config,
        IReadOnlyDictionary<string, string> editCodePerFileHash,
        string folderHash,
        CancellationToken cancellationToken
    );

    Task DownloadFileAsync(
        FilerConfig config,
        string fileHash,
        string targetFilePath,
        ITransferProgress progress,
        long? expectedSizeBytes,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, HostedFileInfo>> GetFileInfoPerFileHashAsync(
        IReadOnlyList<string> fileHashes,
        CancellationToken cancellationToken
    );

    Task EnsureCredentialsAcceptedAsync(FilerConfig config, CancellationToken cancellationToken);
}
