using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Scanning.Repositories;

public interface IRemoteSourceScanRepository
{
    Task<IReadOnlyList<RemoteSourceAutomation>> GetEnabledAutomationsWithActiveRegistrationAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<RemoteSourceDownload>> GetDownloadsAsync(
        int remoteSourceRegistrationId,
        IReadOnlyList<string> remoteFolderPaths,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<RemoteSourceDownload>> GetObservingDownloadsAsync(
        int remoteSourceAutomationId,
        CancellationToken cancellationToken = default
    );

    void Add(RemoteSourceDownload download);

    void Remove(RemoteSourceDownload download);

    void DiscardPendingChanges();

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
