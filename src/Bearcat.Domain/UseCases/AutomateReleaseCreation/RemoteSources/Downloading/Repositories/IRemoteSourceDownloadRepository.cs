using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading.Repositories;

public interface IRemoteSourceDownloadRepository
{
    Task<IReadOnlyList<RemoteSourceDownload>> GetInterruptedDownloadsAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<RemoteSourceDownload>> GetPendingDownloadsAsync(
        CancellationToken cancellationToken = default
    );

    Task<RemoteSourceDownload> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> TryRefreshAsync(
        RemoteSourceDownload download,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
