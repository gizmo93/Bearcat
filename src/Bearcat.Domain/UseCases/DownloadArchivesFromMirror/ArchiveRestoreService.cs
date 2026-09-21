using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Cancellation;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Repositories;
using Bearcat.Domain.ValueObjects;
using Humanizer;
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
    IDownloadCancellationRegistry cancellationRegistry,
    ILogger<ArchiveRestoreService> logger
)
{
    private const int MaxErrorMessageLength = 500;

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
                        .Max(),
                    MirrorPriority: registration.MirrorPriority
                )
            );
        }

        return candidates
            .OrderBy(candidate => candidate.MirrorPriority)
            .ThenByDescending(candidate => candidate.LastCheckedAt)
            .FirstOrDefault();
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

            IReadOnlyList<DownloadResult> results;

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

                results = await RunDownloadsAsync(
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
        IReadOnlyList<DownloadResult> failures,
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
        IReadOnlyList<DownloadResult> failures,
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

    private async Task<IReadOnlyList<DownloadResult>> RunDownloadsAsync(
        int archiveId,
        string hosterName,
        IReadOnlyList<PlannedDownload> downloads,
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
                DownloadAndVerifyAsync(
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

    private async Task<DownloadResult> DownloadAndVerifyAsync(
        int archiveId,
        string hosterName,
        PlannedDownload download,
        IHosterWithDownload hoster,
        IHosterConfig hosterConfig,
        SemaphoreSlim semaphore,
        DownloadSettings downloadSettings,
        CancellationToken cancellationToken
    )
    {
        await semaphore.WaitAsync(cancellationToken);

        var fileName = Path.GetFileName(download.TargetFilePath);
        var maxAttempts = downloadSettings.MaxAttempts;
        var errorMessages = new HashSet<string>();
        var attemptsMade = 0;

        try
        {
            foreach (var attempt in Enumerable.Range(1, maxAttempts))
            {
                attemptsMade = attempt;

                var outcome = await RunDownloadAttemptAsync(
                    archiveId: archiveId,
                    hosterName: hosterName,
                    download: download,
                    hoster: hoster,
                    hosterConfig: hosterConfig,
                    cancellationToken: cancellationToken
                );

                if (outcome.ErrorMessage is null)
                {
                    if (attempt > 1)
                    {
                        logger.LogInformation(
                            "Download of {FileName} from hoster {HosterName} succeeded on attempt {Attempt} of {MaxAttempts}",
                            fileName,
                            hosterName,
                            attempt,
                            maxAttempts
                        );
                    }

                    return DownloadResult.Succeeded(
                        download: download,
                        hosterName: hosterName,
                        md5Hash: outcome.Md5Hash!,
                        attempts: attempt
                    );
                }

                logger.LogWarning(
                    outcome.Exception,
                    "Download attempt {Attempt} of {MaxAttempts} for archive file {ArchiveFileId} ({FileName}) of archive {ArchiveId} from hoster {HosterName} at {HosterFileLink} failed: {ErrorMessage}",
                    attempt,
                    maxAttempts,
                    download.ArchiveFileId,
                    fileName,
                    archiveId,
                    hosterName,
                    download.UploadedFile.HosterFileLink,
                    outcome.ErrorMessage
                );

                if (outcome.IsFileMissing)
                {
                    return DownloadResult.Failed(
                        download: download,
                        hosterName: hosterName,
                        attempts: attempt,
                        isFileMissing: true,
                        errorMessages:
                        [
                            $"The file does not exist on {hosterName} any more: {Truncate(outcome.ErrorMessage)}",
                        ]
                    );
                }
                
                errorMessages.Add(Truncate(outcome.ErrorMessage));

                if (attempt == maxAttempts || cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                logger.LogInformation(
                    "Retrying the download of {FileName} from hoster {HosterName} in {RetryDelaySeconds} seconds (attempt {NextAttempt} of {MaxAttempts})",
                    fileName,
                    hosterName,
                    downloadSettings.RetryDelay.TotalSeconds,
                    attempt + 1,
                    maxAttempts
                );

                if (downloadSettings.RetryDelay > TimeSpan.Zero)
                {
                    await Task.Delay(downloadSettings.RetryDelay, cancellationToken);
                }
            }

            return DownloadResult.Failed(
                download: download,
                hosterName: hosterName,
                attempts: attemptsMade,
                isFileMissing: false,
                errorMessages: errorMessages.ToList()
            );
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<AttemptOutcome> RunDownloadAttemptAsync(
        int archiveId,
        string hosterName,
        PlannedDownload download,
        IHosterWithDownload hoster,
        IHosterConfig hosterConfig,
        CancellationToken cancellationToken
    )
    {
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
                return new AttemptOutcome(
                    Md5Hash: null,
                    ErrorMessage: JoinErrorMessages(result.ErrorMessages),
                    Exception: null,
                    IsFileMissing: result.IsFileMissing
                );
            }

            return await VerifyDownloadAsync(
                download: download,
                hosterName: hosterName,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new AttemptOutcome(
                Md5Hash: null,
                ErrorMessage: ex.InnerException?.Message ?? ex.Message,
                Exception: ex,
                IsFileMissing: false
            );
        }
    }

    private static async Task<AttemptOutcome> VerifyDownloadAsync(
        PlannedDownload download,
        string hosterName,
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
            var sizeOnDisk = new FileInfo(download.TargetFilePath).Length;

            return new AttemptOutcome(
                Md5Hash: null,
                ErrorMessage: $"MD5 mismatch for {Path.GetFileName(download.TargetFilePath)} downloaded from {hosterName}: expected {expectedHash}, got {actualHash} ({sizeOnDisk.Bytes().Humanize("0.0")} on disk)",
                Exception: null,
                IsFileMissing: false
            );
        }

        return new AttemptOutcome(
            Md5Hash: actualHash,
            ErrorMessage: null,
            Exception: null,
            IsFileMissing: false
        );
    }

    private static string JoinErrorMessages(IReadOnlyList<string> errorMessages)
    {
        var joined = string.Join(" | ", errorMessages.Where(message => message.Length > 0));

        return joined.Length > 0 ? joined : "The hoster did not report an error message";
    }

    private static string Truncate(string errorMessage)
    {
        return errorMessage.Length > MaxErrorMessageLength
            ? errorMessage[..MaxErrorMessageLength]
            : errorMessage;
    }

    private sealed record SelectedSourceUpload(
        Upload Upload,
        Dictionary<int, UploadedFile> UploadedFilesByArchiveFileId,
        DateTime? LastCheckedAt,
        int MirrorPriority
    );

    private sealed record PlannedDownload(
        int ArchiveFileId,
        string TargetFilePath,
        long? ExpectedSizeBytes,
        UploadedFile UploadedFile
    );

    private sealed record DownloadSettings(
        int MaxAttempts,
        TimeSpan RetryDelay,
        int MaxParallelDownloads
    );

    private sealed record AttemptOutcome(
        string? Md5Hash,
        string? ErrorMessage,
        Exception? Exception,
        bool IsFileMissing
    );

    private sealed record DownloadResult(
        int ArchiveFileId,
        string TargetFilePath,
        string FileName,
        string HosterName,
        string HosterFileLink,
        bool IsSuccess,
        string? Md5Hash,
        int Attempts,
        bool IsFileMissing,
        IReadOnlyList<string> ErrorMessages
    )
    {
        public string ErrorText => string.Join(" | ", ErrorMessages);

        public static DownloadResult Succeeded(
            PlannedDownload download,
            string hosterName,
            string md5Hash,
            int attempts
        ) =>
            new(
                ArchiveFileId: download.ArchiveFileId,
                TargetFilePath: download.TargetFilePath,
                FileName: Path.GetFileName(download.TargetFilePath),
                HosterName: hosterName,
                HosterFileLink: download.UploadedFile.HosterFileLink,
                IsSuccess: true,
                Md5Hash: md5Hash,
                Attempts: attempts,
                IsFileMissing: false,
                ErrorMessages: []
            );

        public static DownloadResult Failed(
            PlannedDownload download,
            string hosterName,
            int attempts,
            bool isFileMissing,
            IReadOnlyList<string> errorMessages
        ) =>
            new(
                ArchiveFileId: download.ArchiveFileId,
                TargetFilePath: download.TargetFilePath,
                FileName: Path.GetFileName(download.TargetFilePath),
                HosterName: hosterName,
                HosterFileLink: download.UploadedFile.HosterFileLink,
                IsSuccess: false,
                Md5Hash: null,
                Attempts: attempts,
                IsFileMissing: isFileMissing,
                ErrorMessages: errorMessages
            );
    }
}
