using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror;

public class ArchiveRestoreService(
    IArchiveRestoreRepository repository,
    IHosterFactory hosterFactory,
    IFileSystemService fileSystemService,
    ISecretProtector secretProtector,
    INotificationService notificationService,
    IApplicationConfigurationProvider configurationProvider,
    IDownloadProgressTracker downloadProgressTracker,
    ILogger<ArchiveRestoreService> logger
)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await CleanupOrphanedArchiveRestoresAsync(cancellationToken);
        await RestoreWaitingUploadsAsync(cancellationToken);
    }

    private async Task CleanupOrphanedArchiveRestoresAsync(CancellationToken cancellationToken)
    {
        var interruptedArchives = await repository.GetInterruptedRestoresAsync(cancellationToken);

        if (interruptedArchives.Count == 0)
        {
            return;
        }

        foreach (var archive in interruptedArchives)
        {
            logger.LogInformation(
                "Discarding interrupted restore of archive {ArchiveId} at {ArchiveFolderPath}",
                archive.Id,
                archive.ArchiveFolderPath
            );

            fileSystemService.DeleteDirectoryIfExists(archive.ArchiveFolderPath);
            archive.ArchiveState = ArchiveState.Deleted;
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task RestoreWaitingUploadsAsync(CancellationToken cancellationToken)
    {
        var waitingUploads = await repository.GetUploadsWaitingForRestoreAsync(cancellationToken);

        if (waitingUploads.Count == 0)
        {
            return;
        }

        var uploadsByArchiveConfigId = waitingUploads
            .GroupBy(upload => upload.UploadConfig.ArchiveConfigId)
            .ToList();

        foreach (var group in uploadsByArchiveConfigId)
        {
            await RestoreArchiveConfigAsync(
                archiveConfigId: group.Key,
                waitingUploads: group.ToList(),
                cancellationToken: cancellationToken
            );
        }
    }

    private async Task RestoreArchiveConfigAsync(
        int archiveConfigId,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        var archive = await repository.GetLatestArchiveAsync(archiveConfigId, cancellationToken);

        if (archive is null)
        {
            logger.LogWarning(
                "Cannot restore archive for ArchiveConfig {ArchiveConfigId} because it has no archive to restore from",
                archiveConfigId
            );

            return;
        }

        if (archive.ArchiveState is not (ArchiveState.Deleted or ArchiveState.MissingFiles))
        {
            return;
        }

        var uploadsOfArchive = await repository.GetUploadsOfArchiveAsync(
            archive.Id,
            cancellationToken
        );

        var neededArchiveFiles = GetNeededArchiveFiles(
            archive: archive,
            waitingUploads: waitingUploads,
            uploadsOfArchive: uploadsOfArchive
        );

        if (neededArchiveFiles.Count == 0)
        {
            logger.LogInformation(
                "No archive files need to be restored for ArchiveConfig {ArchiveConfigId}",
                archiveConfigId
            );

            return;
        }

        var selectedSourceUpload = FindSourceUpload(
            uploadsOfArchive: uploadsOfArchive,
            neededArchiveFiles: neededArchiveFiles
        );

        if (selectedSourceUpload is null)
        {
            logger.LogWarning(
                "Could not find an online mirror to restore {FileCount} archive files of archive {ArchiveId}",
                neededArchiveFiles.Count,
                archive.Id
            );

            notificationService.Create(
                kind: NotificationKind.ArchiveRestoreFailed,
                message: "No online mirror is available to restore the archive files from.",
                entity: archive,
                selector: n => n.Archive
            );

            FailWaitingUploads(
                waitingUploads,
                "No online mirror is available to restore the archive files from."
            );

            await repository.SaveChangesAsync(cancellationToken);

            return;
        }

        await RestoreFromSourceUploadAsync(
            archive: archive,
            neededArchiveFiles: neededArchiveFiles,
            selectedSourceUpload: selectedSourceUpload,
            waitingUploads: waitingUploads,
            cancellationToken: cancellationToken
        );
    }

    private void FailWaitingUploads(IReadOnlyList<Upload> waitingUploads, string errorMessage)
    {
        foreach (var upload in waitingUploads)
        {
            upload.UploadState = UploadState.Failed;
            upload.ErrorMessages.Add(errorMessage);
        }

        logger.LogInformation(
            "Marked {UploadCount} uploads as failed because their archive could not be restored. Create a manual reupload to retry after fixing the cause",
            waitingUploads.Count
        );
    }

    private static IReadOnlyList<ArchiveFile> GetNeededArchiveFiles(
        Archive archive,
        IReadOnlyList<Upload> waitingUploads,
        IReadOnlyList<Upload> uploadsOfArchive
    )
    {
        var neededArchiveFileIds = new HashSet<int>();

        foreach (var upload in waitingUploads)
        {
            if (upload.UploadConfig.HosterRegistration.AlwaysReuploadAllFiles)
            {
                neededArchiveFileIds.UnionWith(archive.ArchiveFiles.Select(file => file.Id));
                continue;
            }

            var carriedOverArchiveFileIds = GetCarriedOverArchiveFileIds(
                newUpload: upload,
                previousUploads: uploadsOfArchive
            );

            neededArchiveFileIds.UnionWith(
                archive
                    .ArchiveFiles.Where(file => !carriedOverArchiveFileIds.Contains(file.Id))
                    .Select(file => file.Id)
            );
        }

        return archive.ArchiveFiles.Where(file => neededArchiveFileIds.Contains(file.Id)).ToList();
    }

    private static HashSet<int> GetCarriedOverArchiveFileIds(
        Upload newUpload,
        IReadOnlyList<Upload> previousUploads
    )
    {
        return previousUploads
            .Where(upload =>
                upload.Id != newUpload.Id && upload.UploadConfigId == newUpload.UploadConfigId
            )
            .SelectMany(upload => upload.UploadedFiles)
            .GroupBy(uploadedFile => uploadedFile.ArchiveFileId)
            .Select(group => group.MaxBy(uploadedFile => uploadedFile.UploadId)!)
            .Where(uploadedFile =>
                uploadedFile.OnlineState == OnlineState.Online
                && !string.IsNullOrWhiteSpace(uploadedFile.HosterFileLink)
            )
            .Select(uploadedFile => uploadedFile.ArchiveFileId)
            .ToHashSet();
    }

    private SelectedSourceUpload? FindSourceUpload(
        IReadOnlyList<Upload> uploadsOfArchive,
        IReadOnlyList<ArchiveFile> neededArchiveFiles
    )
    {
        var neededArchiveFileIds = neededArchiveFiles.Select(file => file.Id).ToHashSet();

        var candidates = new List<SelectedSourceUpload>();

        foreach (var upload in uploadsOfArchive)
        {
            var registration = upload.UploadConfig.HosterRegistration;

            if (!registration.IsActive || !registration.UseForMirrorDownloads)
            {
                continue;
            }

            if (hosterFactory.GetByName(registration.HosterClassName) is not IHosterWithDownload)
            {
                continue;
            }

            var newestFilePerArchiveFileId = upload
                .UploadedFiles.Where(uploadedFile =>
                    neededArchiveFileIds.Contains(uploadedFile.ArchiveFileId)
                )
                .GroupBy(uploadedFile => uploadedFile.ArchiveFileId)
                .Select(group => group.MaxBy(uploadedFile => uploadedFile.Id)!)
                .Where(uploadedFile =>
                    uploadedFile.OnlineState == OnlineState.Online
                    && !string.IsNullOrWhiteSpace(uploadedFile.HosterFileLink)
                )
                .ToDictionary(uploadedFile => uploadedFile.ArchiveFileId);

            if (newestFilePerArchiveFileId.Count != neededArchiveFileIds.Count)
            {
                continue;
            }

            candidates.Add(
                new SelectedSourceUpload(
                    Upload: upload,
                    UploadedFilesByArchiveFileId: newestFilePerArchiveFileId,
                    LastCheckedAt: newestFilePerArchiveFileId
                        .Values.Select(uploadedFile => uploadedFile.CheckedAt)
                        .Max()
                )
            );
        }

        return candidates.OrderByDescending(candidate => candidate.LastCheckedAt).FirstOrDefault();
    }

    private async Task RestoreFromSourceUploadAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> neededArchiveFiles,
        SelectedSourceUpload selectedSourceUpload,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        var previousArchiveState = archive.ArchiveState;
        var registration = selectedSourceUpload.Upload.UploadConfig.HosterRegistration;
        var hoster = (IHosterWithDownload)hosterFactory.GetByName(registration.HosterClassName);

        var hosterConfig = hoster.DeserializeHosterConfig(
            secretProtector.Unprotect(
                await repository.GetSerializedConfigAsync(registration.Id, cancellationToken)
            )
        );

        var restoreFolderPath = fileSystemService.CreateTempDirectory(
            archive.ArchiveConfig.ArchiveFilesBasePath
        );

        archive.ArchiveState = ArchiveState.Restoring;
        archive.ArchiveFolderPath = restoreFolderPath;

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Restoring {FileCount} archive files of archive {ArchiveId} from upload {DonorUploadId} on hoster {HosterName} into {RestoreFolderPath}",
            neededArchiveFiles.Count,
            archive.Id,
            selectedSourceUpload.Upload.Id,
            registration.Name,
            restoreFolderPath
        );

        var sizePerFileUrl = await GetFileSizesAsync(
            hoster: hoster,
            hosterConfig: hosterConfig,
            fileUrls: neededArchiveFiles
                .Select(archiveFile =>
                    selectedSourceUpload.UploadedFilesByArchiveFileId[archiveFile.Id].HosterFileLink
                )
                .ToList(),
            cancellationToken: cancellationToken
        );

        var downloads = neededArchiveFiles
            .Select(archiveFile =>
            {
                var uploadedFile = selectedSourceUpload.UploadedFilesByArchiveFileId[
                    archiveFile.Id
                ];

                return new PlannedDownload(
                    ArchiveFileId: archiveFile.Id,
                    TargetFilePath: Path.Join(
                        restoreFolderPath,
                        Path.GetFileName(archiveFile.FullFileName)
                    ),
                    ExpectedSizeBytes: sizePerFileUrl.TryGetValue(
                        uploadedFile.HosterFileLink,
                        out var sizeBytes
                    )
                        ? sizeBytes
                        : null,
                    UploadedFile: uploadedFile
                );
            })
            .ToList();

        var results = await RunDownloadsAsync(
            archiveId: archive.Id,
            hosterName: registration.Name,
            downloads: downloads,
            hoster: hoster,
            hosterConfig: hosterConfig,
            cancellationToken: cancellationToken
        );

        var failures = results.Where(result => !result.IsSuccess).ToList();

        if (failures.Count > 0)
        {
            await HandleRestoreFailureAsync(
                archive: archive,
                previousArchiveState: previousArchiveState,
                restoreFolderPath: restoreFolderPath,
                failures: failures,
                waitingUploads: waitingUploads,
                cancellationToken: cancellationToken
            );

            return;
        }

        var archiveFilesById = archive.ArchiveFiles.ToDictionary(file => file.Id);

        foreach (var result in results)
        {
            var archiveFile = archiveFilesById[result.ArchiveFileId];
            archiveFile.FullFileName = result.TargetFilePath;
            archiveFile.Md5Hash = result.Md5Hash;
        }

        archive.ArchiveState = ArchiveState.Created;

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Restored {FileCount} archive files of archive {ArchiveId} into {RestoreFolderPath}",
            results.Count,
            archive.Id,
            restoreFolderPath
        );
    }

    private async Task HandleRestoreFailureAsync(
        Archive archive,
        ArchiveState previousArchiveState,
        string restoreFolderPath,
        IReadOnlyList<DownloadResult> failures,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        var errorMessages = failures.SelectMany(failure => failure.ErrorMessages).ToList();

        logger.LogError(
            "Failed to restore archive {ArchiveId}: {ErrorMessages}",
            archive.Id,
            string.Join(", ", errorMessages)
        );

        fileSystemService.DeleteDirectoryIfExists(restoreFolderPath);

        archive.ArchiveState = previousArchiveState;

        notificationService.Create(
            kind: NotificationKind.ArchiveRestoreFailed,
            message: $"Failed to restore the archive from an online mirror: {string.Join(", ", errorMessages)}",
            entity: archive,
            selector: n => n.Archive
        );

        FailWaitingUploads(
            waitingUploads,
            $"Archive restore failed: {string.Join(", ", errorMessages)}"
        );

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, long>> GetFileSizesAsync(
        IHosterWithDownload hoster,
        IHosterConfig hosterConfig,
        IReadOnlyList<string> fileUrls,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await hoster.GetFileSizesAsync(fileUrls, hosterConfig, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Could not determine the size of {FileCount} mirror files on hoster {HosterName}, the download progress stays indeterminate",
                fileUrls.Count,
                hoster.Name
            );

            return new Dictionary<string, long>();
        }
    }

    private async Task<IReadOnlyList<DownloadResult>> RunDownloadsAsync(
        int archiveId,
        string hosterName,
        IReadOnlyList<PlannedDownload> downloads,
        IHosterWithDownload hoster,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
        var maxParallelDownloads = Math.Max(
            1,
            configurationProvider.GetValue<DownloadConfiguration>(c => c.MaxParallelDownloads)
        );

        using var semaphore = new SemaphoreSlim(maxParallelDownloads);

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
                DownloadAndVerifyAsync(
                    archiveId: archiveId,
                    download: download,
                    hoster: hoster,
                    hosterConfig: hosterConfig,
                    semaphore: semaphore,
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

    private async Task<DownloadResult> DownloadAndVerifyAsync(
        int archiveId,
        PlannedDownload download,
        IHosterWithDownload hoster,
        IHosterConfig hosterConfig,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken
    )
    {
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            var result = await hoster.DownloadFileAsync(
                file: new DownloadFileDto(
                    HosterFileLink: download.UploadedFile.HosterFileLink,
                    ExternalId: download.UploadedFile.ExternalId,
                    ExpectedSizeBytes: download.ExpectedSizeBytes
                ),
                targetFilePath: download.TargetFilePath,
                hosterConfig: hosterConfig,
                progress: new DownloadProgressReporter(
                    tracker: downloadProgressTracker,
                    archiveId: archiveId,
                    archiveFileId: download.ArchiveFileId,
                    fileName: Path.GetFileName(download.TargetFilePath)
                ),
                cancellationToken: cancellationToken
            );

            if (!result.IsSuccess)
            {
                return DownloadResult.Failed(download, result.ErrorMessages);
            }

            return await VerifyDownloadAsync(download, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return DownloadResult.Failed(download, [ex.Message]);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<DownloadResult> VerifyDownloadAsync(
        PlannedDownload download,
        CancellationToken cancellationToken
    )
    {
        var actualHash = await Md5FileHash.ComputeAsync(download.TargetFilePath, cancellationToken);
        var expectedHash = download.UploadedFile.Md5Hash;

        if (
            expectedHash is not null
            && !string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase)
        )
        {
            logger.LogError(
                "Downloaded file {TargetFilePath} has MD5 hash {ActualHash} but the mirror recorded {ExpectedHash}",
                download.TargetFilePath,
                actualHash,
                expectedHash
            );

            return DownloadResult.Failed(
                download,
                [
                    $"MD5 mismatch for {Path.GetFileName(download.TargetFilePath)}: expected {expectedHash}, got {actualHash}",
                ]
            );
        }

        return DownloadResult.Succeeded(download, actualHash);
    }

    private sealed record SelectedSourceUpload(
        Upload Upload,
        Dictionary<int, UploadedFile> UploadedFilesByArchiveFileId,
        DateTime? LastCheckedAt
    );

    private sealed record PlannedDownload(
        int ArchiveFileId,
        string TargetFilePath,
        long? ExpectedSizeBytes,
        UploadedFile UploadedFile
    );

    private sealed record DownloadResult(
        int ArchiveFileId,
        string TargetFilePath,
        bool IsSuccess,
        string? Md5Hash,
        IReadOnlyList<string> ErrorMessages
    )
    {
        public static DownloadResult Succeeded(PlannedDownload download, string md5Hash) =>
            new(
                ArchiveFileId: download.ArchiveFileId,
                TargetFilePath: download.TargetFilePath,
                IsSuccess: true,
                Md5Hash: md5Hash,
                ErrorMessages: []
            );

        public static DownloadResult Failed(
            PlannedDownload download,
            IReadOnlyList<string> errorMessages
        ) =>
            new(
                ArchiveFileId: download.ArchiveFileId,
                TargetFilePath: download.TargetFilePath,
                IsSuccess: false,
                Md5Hash: null,
                ErrorMessages: errorMessages
            );
    }
}
