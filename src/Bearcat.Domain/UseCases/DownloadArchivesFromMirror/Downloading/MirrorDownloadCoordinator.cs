using Bearcat.Domain.Shared.Transfers;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public class MirrorDownloadCoordinator(
    ITransferProgressTracker transferProgressTracker,
    ArchiveFileDownloader archiveFileDownloader
)
{
    public async Task<IReadOnlyList<FileDownloadResult>> RunDownloadsAsync(
        int archiveId,
        IReadOnlyList<PlannedFileDownload> downloads,
        IReadOnlyDictionary<int, ResolvedMirrorHoster> hosters,
        DownloadSettings downloadSettings,
        CancellationToken cancellationToken
    )
    {
        using var semaphore = new SemaphoreSlim(downloadSettings.MaxParallelDownloads);

        var transferIdentifier = new TransferIdentifier(TransferKind.MirrorDownload, archiveId);

        transferProgressTracker.StartTracking(
            identifier: transferIdentifier,
            plannedFiles: downloads
                .Select(download => new TransferFile(
                    FileId: download.ArchiveFileId,
                    FileName: Path.GetFileName(download.TargetFilePath),
                    SourceName: download.Sources[0].Registration.Name,
                    SizeBytes: download.ExpectedSizeBytes,
                    IsAlreadyTransferred: false
                ))
                .ToList()
        );

        try
        {
            var downloadTasks = downloads.Select(download =>
                archiveFileDownloader.DownloadAndVerifyAsync(
                    archiveId: archiveId,
                    download: download,
                    hosters: hosters,
                    semaphore: semaphore,
                    downloadSettings: downloadSettings,
                    cancellationToken: cancellationToken
                )
            );

            return await Task.WhenAll(downloadTasks);
        }
        finally
        {
            transferProgressTracker.StopTracking(transferIdentifier);
        }
    }
}
