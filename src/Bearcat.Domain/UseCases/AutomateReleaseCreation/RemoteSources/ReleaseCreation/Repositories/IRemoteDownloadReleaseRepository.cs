using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.ReleaseCreation.Repositories;

public interface IRemoteDownloadReleaseRepository
{
    Task<IReadOnlyList<RemoteSourceDownload>> GetDownloadedWithoutReleaseAsync(
        CancellationToken cancellationToken = default
    );

    Task<ReleaseTemplate?> GetReleaseTemplateAsync(
        int releaseTemplateId,
        CancellationToken cancellationToken = default
    );

    void Add(Release release);

    void DiscardPendingChanges();

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
