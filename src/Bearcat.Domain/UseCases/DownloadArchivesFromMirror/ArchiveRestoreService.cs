using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
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
    ITransferCancellationRegistry cancellationRegistry,
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

        var plan = mirrorSourceResolver.ResolveSources(
            uploadsOfArchive: uploadsOfArchive,
            neededArchiveFiles: neededArchiveFiles
        );

        if (plan.FilesWithoutSource.Count > 0)
        {
            var release = waitingUploads[0].UploadConfig.Release;

            var fileNamesWithoutSource = string.Join(
                ", ",
                plan.FilesWithoutSource.Select(file => Path.GetFileName(file.FullFileName))
            );

            if (release.ReleaseType is ReleaseType.Managed)
            {
                logger.LogInformation(
                    "No online mirror is available to restore {FileCount} archive files ({FileNames}) of archive {ArchiveId}, the release will be repackaged from the release folder instead",
                    plan.FilesWithoutSource.Count,
                    fileNamesWithoutSource,
                    archive.Id
                );

                return;
            }

            if (archive.ArchiveState is ArchiveState.MissingFiles)
            {
                logger.LogInformation(
                    "No online mirror is available to restore {FileCount} archive files ({FileNames}) of archive {ArchiveId}, waiting for the user to provide the archive files",
                    plan.FilesWithoutSource.Count,
                    fileNamesWithoutSource,
                    archive.Id
                );

                return;
            }

            logger.LogWarning(
                "Could not find an online mirror to restore {FileCount} archive files ({FileNames}) of archive {ArchiveId}",
                plan.FilesWithoutSource.Count,
                fileNamesWithoutSource,
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

        await RestoreFromMirrorsAsync(
            archive: archive,
            neededArchiveFiles: neededArchiveFiles,
            plan: plan,
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

    private async Task RestoreFromMirrorsAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> neededArchiveFiles,
        MirrorSourcePlan plan,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        var previousArchiveState = archive.ArchiveState;
        var downloadSettings = ReadDownloadSettings();
        var hosters = await ResolveHostersAsync(plan, cancellationToken);

        var restoreFolderPath = fileSystemService.CreateTempDirectory(
            archive.ArchiveConfig.ArchiveFilesBasePath
        );

        archive.ArchiveState = ArchiveState.Restoring;
        archive.ArchiveFolderPath = restoreFolderPath;

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Restoring {FileCount} archive files of archive {ArchiveId} from the hosters {HosterNames} into {RestoreFolderPath}",
            neededArchiveFiles.Count,
            archive.Id,
            string.Join(", ", hosters.Values.Select(resolved => resolved.Registration.Name)),
            restoreFolderPath
        );

        var transferIdentifier = new TransferIdentifier(TransferType.MirrorDownload, archive.Id);
        var userCancellationToken = cancellationRegistry.Register(transferIdentifier);

        try
        {
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                userCancellationToken
            );

            IReadOnlyList<FileDownloadResult> results;

            try
            {
                var sizePerFileUrl = await GetFirstSourceFileSizesAsync(
                    neededArchiveFiles: neededArchiveFiles,
                    plan: plan,
                    hosters: hosters,
                    cancellationToken: linkedTokenSource.Token
                );

                var downloads = neededArchiveFiles
                    .Select(archiveFile =>
                    {
                        var sources = plan.SourcesPerArchiveFileId[archiveFile.Id];

                        return new PlannedFileDownload(
                            ArchiveFileId: archiveFile.Id,
                            TargetFilePath: Path.Join(
                                restoreFolderPath,
                                Path.GetFileName(archiveFile.FullFileName)
                            ),
                            ExpectedSizeBytes: sizePerFileUrl.TryGetValue(
                                sources[0].UploadedFile.HosterFileLink,
                                out var sizeBytes
                            )
                                ? sizeBytes
                                : null,
                            Sources: sources
                        );
                    })
                    .ToList();

                results = await mirrorDownloadCoordinator.RunDownloadsAsync(
                    archiveId: archive.Id,
                    downloads: downloads,
                    hosters: hosters,
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
            cancellationRegistry.Unregister(transferIdentifier);
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
        int maxAttempts,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        foreach (var failure in failures)
        {
            logger.LogError(
                "Failed to download archive file {ArchiveFileId} ({FileName}) of archive {ArchiveId} from any online mirror: {FailureChain}",
                failure.ArchiveFileId,
                failure.FileName,
                archive.Id,
                failure.ErrorText
            );
        }

        logger.LogError(
            "Failed to restore archive {ArchiveId}: {FailedFileCount} of {TotalFileCount} archive files could not be downloaded from any online mirror",
            archive.Id,
            failures.Count,
            totalFileCount
        );

        fileSystemService.DeleteDirectoryIfExists(restoreFolderPath);

        archive.ArchiveState = previousArchiveState;

        var errorMessage = BuildFailureMessage(
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
        IReadOnlyList<FileDownloadResult> failures,
        int totalFileCount,
        int maxAttempts
    )
    {
        var fileDetails = string.Join(
            "; ",
            failures.Select(failure => $"{failure.FileName}: {failure.ErrorText}")
        );

        return $"Archive restore failed: {failures.Count} of {totalFileCount} archive files could not be downloaded from any online mirror (up to {maxAttempts} attempts per mirror). {fileDetails}";
    }

    private async Task<Dictionary<int, ResolvedMirrorHoster>> ResolveHostersAsync(
        MirrorSourcePlan plan,
        CancellationToken cancellationToken
    )
    {
        var registrations = plan
            .SourcesPerArchiveFileId.Values.SelectMany(selector: sources => sources)
            .Select(selector: source => source.Registration)
            .DistinctBy(keySelector: registration => registration.Id)
            .ToList();

        var hosters = new Dictionary<int, ResolvedMirrorHoster>();

        foreach (var registration in registrations)
        {
            var hoster = (IHosterWithDownload)
                hosterFactory.GetByName(name: registration.HosterClassName);

            var hosterConfig = hoster.DeserializeHosterConfig(
                serializedConfig: secretProtector.Unprotect(
                    protectedValue: await repository.GetSerializedConfigAsync(
                        hosterRegistrationId: registration.Id,
                        cancellationToken: cancellationToken
                    )
                )
            );

            hosters[key: registration.Id] = new ResolvedMirrorHoster(
                Registration: registration,
                Hoster: hoster,
                Config: hosterConfig
            );
        }

        return hosters;
    }

    private async Task<Dictionary<string, long>> GetFirstSourceFileSizesAsync(
        IReadOnlyList<ArchiveFile> neededArchiveFiles,
        MirrorSourcePlan plan,
        IReadOnlyDictionary<int, ResolvedMirrorHoster> hosters,
        CancellationToken cancellationToken
    )
    {
        var sizePerFileUrl = new Dictionary<string, long>();

        var firstSourcesPerRegistrationId = neededArchiveFiles
            .Select(archiveFile => plan.SourcesPerArchiveFileId[archiveFile.Id][0])
            .GroupBy(source => source.Registration.Id);

        foreach (var group in firstSourcesPerRegistrationId)
        {
            var resolved = hosters[group.Key];

            var sizes = await GetFileSizesAsync(
                hoster: resolved.Hoster,
                hosterConfig: resolved.Config,
                fileUrls: group.Select(source => source.UploadedFile.HosterFileLink).ToList(),
                cancellationToken: cancellationToken
            );

            foreach (var entry in sizes)
            {
                sizePerFileUrl[entry.Key] = entry.Value;
            }
        }

        return sizePerFileUrl;
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
