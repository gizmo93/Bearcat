using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.RemoteSource.Dto;
using Bearcat.Abstractions.Transfers;

namespace Bearcat.Domain.IntegrationTest.UseCases.AutomateReleaseCreation.RemoteSources;

public sealed class FakeRemoteSourceSession(FakeRemoteServer server) : IRemoteSourceSession
{
    public Task<IReadOnlyList<RemoteFolderDto>> ListFoldersAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(server.ListFolders(path));
    }

    public Task<IReadOnlyList<RemoteFileDto>> ListFilesRecursiveAsync(
        string folderPath,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(server.ListFilesRecursive(folderPath));
    }

    public Task DownloadFileAsync(
        RemoteFileDto file,
        string localFilePath,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        return server.DownloadAsync(file, localFilePath, progress, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        server.MarkDisposed();

        return ValueTask.CompletedTask;
    }
}
