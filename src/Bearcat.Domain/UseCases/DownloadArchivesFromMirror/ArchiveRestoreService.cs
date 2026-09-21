using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Cancellation;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Repositories;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Sources;
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
    IDownloadCancellationRegistry cancellationRegistry,
    MirrorSourceResolver mirrorSourceResolver,
    MirrorDownloadCoordinator mirrorDownloadCoordinator,
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

        var neededArchiveFiles = MirrorSourceResolver.GetNeededArchiveFiles(
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

        var selectedSourceUpload = mirrorSourceResolver.FindSourceUpload(
            uploadsOfArchive: uploadsOfArchive,
            neededArchiveFiles: neededArchiveFiles
        );

        if (selectedSourceUpload is null)
        {
            var release = waitingUploads[0].UploadConfig.Release;

            if (release.ReleaseType is ReleaseType.Managed)
            {
                logger.LogInformation(
                    "No online mirror is available to restore {FileCount} archive files of archive {ArchiveId}, the release will be repackaged from the release folder instead",
                    neededArchiveFiles.Count,
                    archive.Id
                );

                return;
            }

            if (archive.ArchiveState is ArchiveState.MissingFiles)
            {
                logger.LogInformation(
                    "No online mirror is available to restore {FileCount} archive files of archive {ArchiveId}, waiting for the user to provide the archive files",
                    neededArchiveFiles.Count,
                    archive.Id
                );

                return;
            }

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
        var downloadSettings = ReadDownloadSettings();

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

        var userCancellationToken = cancellationRegistry.Register(archive.Id);

        try
        {
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                userCancellationToken
            );

            IReadOnlyList<FileDownloadResult> results;

            try
            {
                var sizePerFileUrl = await GetFileSizesAsync(
                    hoster: hoster,
                    hosterConfig: hosterConfig,
                    fileUrls: neededArchiveFiles
                        .Select(archiveFile =>
                            selectedSourceUpload
                                .UploadedFilesByArchiveFileId[archiveFile.Id]
                                .HosterFileLink
                        )
                        .ToList(),
                    cancellationToken: linkedTokenSource.Token
                );

                var downloads = neededArchiveFiles
                    .Select(archiveFile =>
                    {
                        var uploadedFile = selectedSourceUpload.UploadedFilesByArchiveFileId[
                            archiveFile.Id
                        ];

                        return new PlannedFileDownload(
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

                results = await mirrorDownloadCoordinator.RunDownloadsAsync(
                    archiveId: archive.Id,
                    hosterName: registration.Name,
                    downloads: downloads,
                    hoster: hoster,
                    hosterConfig: hosterConfig,
                    downloadSettings: downloadSettings,
                    cancellationToken: linkedTokenSource.Token
                );
            }
            catch (OperationCanceledException) when (userCancellationToken.IsCancellationRequested)
            {
                await HandleRestoreCancellationAsync(
                    archive: archive,
                    previousArchiveState: previousArchiveState,
                    restoreFolderPath: restoreFolderPath,
                    waitingUploads: waitingUploads,
                    cancellationToken: cancellationToken
                );

                return;
            }

            var failures = results.Where(result => !result.IsSuccess).ToList();

            if (failures.Count > 0)
            {
                if (userCancellationToken.IsCancellationRequested)
                {
                    await HandleRestoreCancellationAsync(
                        archive: archive,
                        previousArchiveState: previousArchiveState,
                        restoreFolderPath: restoreFolderPath,
                        waitingUploads: waitingUploads,
                        cancellationToken: cancellationToken
                    );

                    return;
                }

                await HandleRestoreFailureAsync(
                    archive: archive,
                    previousArchiveState: previousArchiveState,
                    restoreFolderPath: restoreFolderPath,
                    failures: failures,
                    totalFileCount: results.Count,
                    hosterName: registration.Name,
                    sourceUploadId: selectedSourceUpload.Upload.Id,
                    maxAttempts: downloadSettings.MaxAttempts,
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
        finally
        {
            cancellationRegistry.Unregister(archive.Id);
        }
    }

    private async Task HandleRestoreCancellationAsync(
        Archive archive,
        ArchiveState previousArchiveState,
        string restoreFolderPath,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Canceled the restore of archive {ArchiveId} on user request, discarding the partial download in {RestoreFolderPath}",
            archive.Id,
            restoreFolderPath
        );

        fileSystemService.DeleteDirectoryIfExists(restoreFolderPath);

        archive.ArchiveState = previousArchiveState;

        foreach (var upload in waitingUploads)
        {
            upload.UploadState = UploadState.Canceled;

            notificationService.Create(
                kind: NotificationKind.UploadCanceled,
                message: "Upload canceled because the archive restore was canceled.",
                entity: upload,
                selector: n => n.Upload
            );
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Canceled {UploadCount} uploads that were waiting for the restored archive",
            waitingUploads.Count
        );
    }

    private async Task HandleRestoreFailureAsync(
        Archive archive,
        ArchiveState previousArchiveState,
        string restoreFolderPath,
        IReadOnlyList<FileDownloadResult> failures,
        int totalFileCount,
        string hosterName,
        int sourceUploadId,
        int maxAttempts,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        foreach (var failure in failures)
        {
            logger.LogError(
                "Failed to download archive file {ArchiveFileId} ({FileName}) of archive {ArchiveId} from hoster {HosterName} at {HosterFileLink} after {Attempts} attempts: {ErrorMessage}",
                failure.ArchiveFileId,
                failure.FileName,
                archive.Id,
                failure.HosterName,
                failure.HosterFileLink,
                failure.Attempts,
                failure.ErrorText
            );
        }

        logger.LogError(
            "Failed to restore archive {ArchiveId} from hoster {HosterName} (source upload {SourceUploadId}): {FailedFileCount} of {TotalFileCount} archive files could not be downloaded",
            archive.Id,
            hosterName,
            sourceUploadId,
            failures.Count,
            totalFileCount
        );

        fileSystemService.DeleteDirectoryIfExists(restoreFolderPath);

        archive.ArchiveState = previousArchiveState;

        var errorMessage = BuildFailureMessage(
            hosterName: hosterName,
            failures: failures,
            totalFileCount: totalFileCount,
            maxAttempts: maxAttempts
        );

        notificationService.Create(
            kind: NotificationKind.ArchiveRestoreFailed,
            message: errorMessage,
            entity: archive,
            selector: n => n.Archive
        );

        FailWaitingUploads(waitingUploads, errorMessage);

        await repository.SaveChangesAsync(cancellationToken);
    }

    private static string BuildFailureMessage(
        string hosterName,
        IReadOnlyList<FileDownloadResult> failures,
        int totalFileCount,
        int maxAttempts
    )
    {
        var fileDetails = string.Join(
            "; ",
            failures.Select(failure =>
                $"{failure.FileName} (failed after {failure.Attempts} of {maxAttempts} attempts): {failure.ErrorText}"
            )
        );

        return $"Archive restore failed: {failures.Count} of {totalFileCount} archive files could not be downloaded from the mirror on {hosterName} with up to {maxAttempts} attempts per file. {fileDetails}";
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

    private DownloadSettings ReadDownloadSettings()
    {
        var configuration = configurationProvider.GetConfiguration<DownloadConfiguration>();

        return new DownloadSettings(
            MaxAttempts: Math.Max(1, configuration.MaxDownloadAttempts),
            RetryDelay: TimeSpan.FromSeconds(configuration.DownloadRetryDelaySeconds),
            MaxParallelDownloads: Math.Max(1, configuration.MaxParallelDownloads)
        );
    }
}
