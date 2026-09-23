using Bearcat.Abstractions.RemoteSource.Dto;
using Bearcat.Abstractions.Transfers;

namespace Bearcat.Abstractions.RemoteSource;

public interface IRemoteSourceSession : IAsyncDisposable
{
    Task<IReadOnlyList<RemoteFolderDto>> ListFoldersAsync(
        string path,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<RemoteFileDto>> ListFilesRecursiveAsync(
        string folderPath,
        CancellationToken cancellationToken
    );

    Task DownloadFileAsync(
        RemoteFileDto file,
        string localFilePath,
        ITransferProgress progress,
        CancellationToken cancellationToken
    );
}
