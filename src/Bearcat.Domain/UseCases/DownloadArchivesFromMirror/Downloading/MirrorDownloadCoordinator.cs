using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public class MirrorDownloadCoordinator(
    IDownloadProgressTracker downloadProgressTracker,
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

        downloadProgressTracker.StartTracking(
            archiveId: archiveId,
            plannedFiles: downloads
                .Select(download => new PlannedDownloadFile(
                    ArchiveFileId: download.ArchiveFileId,
                    FileName: Path.GetFileName(download.TargetFilePath),
                    HosterName: download.Sources[0].Registration.Name,
                    SizeBytes: download.ExpectedSizeBytes
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
            downloadProgressTracker.StopTracking(archiveId);
        }
    }
}
