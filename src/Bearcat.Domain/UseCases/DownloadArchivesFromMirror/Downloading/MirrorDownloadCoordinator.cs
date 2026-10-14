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
        var transferIdentifier = new TransferIdentifier(TransferType.MirrorDownload, archiveId);

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
            var results = new FileDownloadResult[downloads.Count];

            await Parallel.ForEachAsync(
                source: Enumerable.Range(0, downloads.Count),
                parallelOptions: new ParallelOptions
                {
                    MaxDegreeOfParallelism = downloadSettings.MaxParallelDownloads,
                    CancellationToken = cancellationToken,
                },
                body: async (downloadIndex, downloadCancellationToken) =>
                {
                    results[downloadIndex] = await archiveFileDownloader.DownloadAndVerifyAsync(
                        archiveId: archiveId,
                        download: downloads[downloadIndex],
                        hosters: hosters,
                        downloadSettings: downloadSettings,
                        cancellationToken: downloadCancellationToken
                    );
                }
            );

            return results;
        }
        finally
        {
            transferProgressTracker.StopTracking(transferIdentifier);
        }
    }
}
