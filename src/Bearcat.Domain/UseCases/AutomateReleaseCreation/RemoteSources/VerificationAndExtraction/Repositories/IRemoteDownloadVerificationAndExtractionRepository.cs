using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction.Repositories;

public interface IRemoteDownloadVerificationAndExtractionRepository
{
    Task<
        IReadOnlyList<RemoteSourceDownload>
    > GetDownloadsInterruptedDuringVerificationOrExtractionAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<RemoteSourceDownload>> GetDownloadsInDownloadedStateAsync(
        CancellationToken cancellationToken = default
    );

    Task<bool> TryRefreshAsync(
        RemoteSourceDownload download,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
