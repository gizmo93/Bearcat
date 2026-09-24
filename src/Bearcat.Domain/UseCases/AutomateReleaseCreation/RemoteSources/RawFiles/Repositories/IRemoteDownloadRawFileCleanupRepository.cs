using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.RawFiles.Repositories;

public interface IRemoteDownloadRawFileCleanupRepository
{
    Task<IReadOnlyList<RemoteSourceDownload>> GetRawFileCleanupCandidatesAsync(
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
