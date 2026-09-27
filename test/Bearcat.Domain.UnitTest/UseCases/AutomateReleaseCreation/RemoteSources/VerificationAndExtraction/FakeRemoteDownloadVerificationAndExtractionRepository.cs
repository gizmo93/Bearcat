using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UnitTest.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction;

public sealed class FakeRemoteDownloadVerificationAndExtractionRepository
    : IRemoteDownloadVerificationAndExtractionRepository
{
    public List<RemoteSourceDownload> Downloads { get; } = [];

    public List<RemoteSourceDownloadState> SavedStates { get; } = [];

    public Task<
        IReadOnlyList<RemoteSourceDownload>
    > GetDownloadsInterruptedDuringVerificationOrExtractionAsync(
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyList<RemoteSourceDownload>>(
            Downloads
                .Where(download =>
                    download.State
                        is RemoteSourceDownloadState.Verifying
                            or RemoteSourceDownloadState.Extracting
                )
                .ToList()
        );
    }

    public Task<IReadOnlyList<RemoteSourceDownload>> GetDownloadsInDownloadedStateAsync(
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IReadOnlyList<RemoteSourceDownload>>(
            Downloads
                .Where(download => download.State is RemoteSourceDownloadState.Downloaded)
                .OrderBy(download => download.CompletedAt)
                .ThenBy(download => download.Id)
                .ToList()
        );
    }

    public Task<bool> TryRefreshAsync(
        RemoteSourceDownload download,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(Downloads.Contains(download));
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SavedStates.AddRange(Downloads.Select(download => download.State));

        return Task.CompletedTask;
    }
}
