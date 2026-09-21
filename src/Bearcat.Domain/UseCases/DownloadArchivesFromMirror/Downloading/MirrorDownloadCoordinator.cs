using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public class MirrorDownloadCoordinator(
    IDownloadProgressTracker downloadProgressTracker,
    ArchiveFileDownloader archiveFileDownloader
)
{
    public async Task<IReadOnlyList<FileDownloadResult>> RunDownloadsAsync(
        int archiveId,
        string hosterName,
        IReadOnlyList<PlannedFileDownload> downloads,
        IHosterWithDownload hoster,
        IHosterConfig hosterConfig,
        DownloadSettings downloadSettings,
        CancellationToken cancellationToken
    )
    {
        using var semaphore = new SemaphoreSlim(downloadSettings.MaxParallelDownloads);

        downloadProgressTracker.StartTracking(
            archiveId,
            hosterName,
            downloads
                .Select(download => new PlannedDownloadFile(
                    ArchiveFileId: download.ArchiveFileId,
                    FileName: Path.GetFileName(download.TargetFilePath),
                    SizeBytes: download.ExpectedSizeBytes
                ))
                .ToList()
        );

        try
        {
            var downloadTasks = downloads.Select(download =>
                archiveFileDownloader.DownloadAndVerifyAsync(
                    archiveId: archiveId,
                    hosterName: hosterName,
                    download: download,
                    hoster: hoster,
                    hosterConfig: hosterConfig,
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
