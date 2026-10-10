using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Transfers;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageArchives.Reuploads;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageArchives;

public class ArchiveCreationService(
    IArchiveCreationRepository repository,
    ILogger<ArchiveCreationService> logger,
    IArchiverFactory archiverFactory,
    IFileSystemService fileSystemService,
    TimeProvider timeProvider,
    INotificationService notificationService,
    IApplicationConfigurationProvider configurationProvider,
    ReleaseFolderEntriesForPackingService releaseFolderEntriesForPackingService,
    ITransferProgressTracker progressTracker,
    ITransferCancellationRegistry cancellationRegistry,
    FolderSizeProgressReporter folderSizeProgressReporter
)
{
    private const string SynologyMetadataFolderName = "@eaDir";

    private readonly Lock knownHashesLock = new();

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await HandleInterruptedArchivesAsync(cancellationToken);
        await ProcessUploadsWithoutArchiveAsync(cancellationToken);
    }

    private async Task HandleInterruptedArchivesAsync(CancellationToken cancellationToken)
    {
        var interruptedArchives = await repository.GetInterruptedArchivesAsync(cancellationToken);

        foreach (var archive in interruptedArchives)
        {
            DeleteReleaseFolderEntriesCopiedForPacking(archive);

            if (AllArchiveFilesExistOnDisk(archive))
            {
                await RecoverInterruptedArchiveAsync(archive, cancellationToken);
            }
            else
            {
                DeleteInterruptedArchive(archive);
            }

            await repository.SaveChangesAsync(cancellationToken: cancellationToken);
        }
    }

    private void DeleteReleaseFolderEntriesCopiedForPacking(Archive archive)
    {
        if (archive.ReleaseFolderEntriesCopiedForPacking.Count == 0)
        {
            return;
        }

        var releaseFolderPath = archive.ArchiveConfig.Release.ReleaseFolderPath;

        if (releaseFolderPath is null)
        {
            logger.LogWarning(
                "Cannot delete entries {EntryNames} copied for packing interrupted archive {ArchiveId} because its release no longer has a release folder",
                archive.ReleaseFolderEntriesCopiedForPacking,
                archive.Id
            );
        }
        else
        {
            logger.LogInformation(
                "Deleting entries {EntryNames} copied into release folder {ReleaseFolderPath} for packing interrupted archive {ArchiveId}",
                archive.ReleaseFolderEntriesCopiedForPacking,
                releaseFolderPath,
                archive.Id
            );

            releaseFolderEntriesForPackingService.DeleteFromReleaseFolder(
                releaseFolderPath,
                archive.ReleaseFolderEntriesCopiedForPacking
            );
        }

        archive.ReleaseFolderEntriesCopiedForPacking = [];
    }

    private static bool AllArchiveFilesExistOnDisk(Archive archive)
    {
        return archive.ArchiveFiles.Count > 0
            && archive.ArchiveFiles.All(f => File.Exists(f.FullFileName));
    }

    private async Task RecoverInterruptedArchiveAsync(
        Archive archive,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Recovering interrupted archive {ArchiveId} by rehashing its {FileCount} existing files instead of repacking",
            archive.Id,
            archive.ArchiveFiles.Count
        );

        var archiver = archiverFactory.GetByName(archive.ArchiveConfig.ArchiverName);

        await HashArchiveFilesAsync(archive, archive.ArchiveConfig, archiver, cancellationToken);
        FinalizeArchive(archive);
    }

    private void DeleteInterruptedArchive(Archive archive)
    {
        logger.LogInformation(
            "Deleting interrupted archive {ArchiveId} because its files are incomplete on disk. Assigned uploads will be repacked",
            archive.Id
        );

        DeleteArchiveFolderAndRemoveArchive(archive);
    }

    private void DeleteArchiveFolderAndRemoveArchive(Archive archive)
    {
        fileSystemService.DeleteDirectoryIfExists(archive.ArchiveFolderPath);
        repository.Remove(archive);
    }

    private static void FinalizeArchive(Archive archive)
    {
        archive.ArchiveState = ArchiveState.Created;

        foreach (
            var upload in archive.Uploads.Where(u => u.UploadState == UploadState.WaitingForArchive)
        )
        {
            upload.UploadState = UploadState.Pending;
        }
    }

    private async Task HashArchiveFilesAsync(
        Archive archive,
        ArchiveConfig archiveConfig,
        IArchiver archiver,
        CancellationToken cancellationToken
    )
    {
        if (archiver.CanChangeHashInPlace)
        {
            await ChangeArchiveFileHashesAsync(
                archive: archive,
                archiveFiles: archive.ArchiveFiles,
                archiveConfig: archiveConfig,
                knownHashes: await LoadKnownHashesAsync(archiveConfig.Id, cancellationToken),
                transferType: TransferType.ArchiveHashing,
                cancellationToken: cancellationToken
            );
        }
        else
        {
            await StoreArchiveFileHashesAsync(archive, archiveConfig, cancellationToken);
        }
    }

    private async Task ProcessUploadsWithoutArchiveAsync(CancellationToken cancellationToken)
    {
        var uploadsWithoutArchive = await repository.GetUploadsWithoutArchiveAsync(
            cancellationToken
        );
        var archivesToCreate = new Dictionary<ArchiveConfig, List<Upload>>();
        var uploadsByArchiveConfigId = new Dictionary<int, List<Upload>>();
        var archiveConfigsById = new Dictionary<int, ArchiveConfig>();

        foreach (var upload in uploadsWithoutArchive)
        {
            archiveConfigsById.TryAdd(
                upload.UploadConfig.ArchiveConfigId,
                upload.UploadConfig.ArchiveConfig
            );

            if (!uploadsByArchiveConfigId.TryAdd(upload.UploadConfig.ArchiveConfigId, [upload]))
            {
                uploadsByArchiveConfigId[upload.UploadConfig.ArchiveConfigId].Add(upload);
            }
        }

        foreach (var (archiveConfigId, uploads) in uploadsByArchiveConfigId)
        {
            logger.LogInformation(
                "Processing uploads {UploadIds} for UploadConfig {UploadConfigIds} without archive",
                uploads.Select(u => u.Id),
                uploads.Select(u => u.UploadConfigId)
            );

            var archiveConfig = archiveConfigsById[archiveConfigId];
            var existingArchiveWasHandled = await TryAssignExistingArchiveAsync(
                archiveConfig,
                uploads,
                cancellationToken
            );

            if (existingArchiveWasHandled)
            {
                continue;
            }

            // We only create new archives for managed releases, for unmanaged releases ("bring your own archives")
            // there is always an ArchiveConfig + Archive existing
            if (uploads.First().UploadConfig.Release.ReleaseType is ReleaseType.Managed)
            {
                archivesToCreate.Add(archiveConfig, uploads);
            }
        }

        if (archivesToCreate.Count == 0)
        {
            return;
        }

        logger.LogInformation(
            "Creating {ArchiveCount} new archives for uploads",
            archivesToCreate.Count
        );

        foreach (var (archiveConfig, uploads) in archivesToCreate)
        {
            await CreateArchiveAsync(archiveConfig, uploads, cancellationToken);
        }
    }

    private async Task<bool> TryAssignExistingArchiveAsync(
        ArchiveConfig archiveConfig,
        IReadOnlyList<Upload> uploads,
        CancellationToken cancellationToken
    )
    {
        var assignableArchive = await repository.GetPossibleAssignableArchiveAsync(
            archiveConfigId: archiveConfig.Id,
            cancellationToken: cancellationToken
        );

        if (assignableArchive is null)
        {
            logger.LogInformation(
                "Could not find existing archive for ArchiveConfig {ArchiveConfigId}",
                archiveConfig.Id
            );

            return false;
        }

        var archiveFilesToUpload = ArchiveFilesToUpload.GetArchiveFilesToUpload(
            archiveFiles: assignableArchive.ArchiveFiles,
            uploads: uploads,
            uploadsOfArchive: assignableArchive.Uploads
        );

        var archiveNeedsHashChange = await ArchiveNeedsHashChangeAsync(
            archiveConfig: archiveConfig,
            uploads: uploads,
            cancellationToken: cancellationToken
        );

        var archiveFilesToUploadNeedNewHashes =
            archiveNeedsHashChange && archiveFilesToUpload.Count > 0;
        var archiver = archiverFactory.GetByName(archiveConfig.ArchiverName);

        if (archiveFilesToUploadNeedNewHashes && !archiver.CanChangeHashInPlace)
        {
            logger.LogInformation(
                "Archiver {ArchiverName} does not support changing hashes in place. Creating a new archive instead of reusing archive {ArchiveId}.",
                archiver.Name,
                assignableArchive.Id
            );

            return false;
        }

        var archiveFilesToChangeHash = archiver.CanChangeHashInPlace
            ? assignableArchive
                .ArchiveFiles.Where(archiveFile =>
                    (
                        archiveFilesToUploadNeedNewHashes
                        && archiveFilesToUpload.Contains(archiveFile)
                    ) || archiveFile.Md5Hash is null
                )
                .ToList()
            : [];

        var missingArchiveFiles = archiveFilesToUpload
            .Where(archiveFile => !fileSystemService.FileExists(archiveFile.FullFileName))
            .ToList();

        if (missingArchiveFiles.Count > 0 || archiveFilesToChangeHash.Count > 0)
        {
            var archiveHasActiveUpload = await repository.HasActiveUploadAsync(
                archiveId: assignableArchive.Id,
                cancellationToken: cancellationToken
            );

            if (archiveHasActiveUpload)
            {
                logger.LogInformation(
                    "Existing archive {ArchiveId} is currently used by an active upload. Skipping {UploadCount} uploads until the next archive creation run",
                    assignableArchive.Id,
                    uploads.Count
                );

                return true;
            }
        }

        if (missingArchiveFiles.Count > 0)
        {
            await MarkArchiveFilesMissingAsync(
                archive: assignableArchive,
                missingArchiveFiles: missingArchiveFiles,
                waitingUploads: uploads,
                cancellationToken: cancellationToken
            );

            return true;
        }

        if (archiveFilesToChangeHash.Count > 0)
        {
            var hashesWereChanged =
                await ChangeExistingArchiveHashesOrCancelWaitingUploadsOnUserCancellationAsync(
                    archive: assignableArchive,
                    archiveFiles: archiveFilesToChangeHash,
                    archiveConfig: archiveConfig,
                    waitingUploads: uploads,
                    cancellationToken: cancellationToken
                );

            if (!hashesWereChanged)
            {
                return true;
            }
        }

        foreach (var upload in uploads)
        {
            upload.ArchiveId = assignableArchive.Id;
            upload.UploadState = UploadState.Pending;

            CarryOverOnlineFiles(upload, assignableArchive);
        }

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        logger.LogInformation(
            "Assigned existing archive {ArchiveId} to {UploadCount} uploads",
            assignableArchive.Id,
            uploads.Count
        );

        return true;
    }

    private async Task MarkArchiveFilesMissingAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> missingArchiveFiles,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Archive files {FilePaths} needed by the waiting uploads are missing in existing archive {ArchiveId}. Marking the archive for restore before changing any hashes",
            string.Join(", ", missingArchiveFiles.Select(archiveFile => archiveFile.FullFileName)),
            archive.Id
        );

        archive.ArchiveState = ArchiveState.MissingFiles;

        foreach (var upload in waitingUploads)
        {
            notificationService.Create(
                kind: NotificationKind.ArchiveFilesMissing,
                message: ArchiveFilesMissingNotificationMessage.Get(
                    upload.UploadConfig.Release.ReleaseType
                ),
                entity: upload,
                selector: n => n.Upload
            );
        }

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }

    private async Task<bool> ChangeExistingArchiveHashesOrCancelWaitingUploadsOnUserCancellationAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> archiveFiles,
        ArchiveConfig archiveConfig,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        var transferIdentifier = new TransferIdentifier(TransferType.ArchiveCreation, archive.Id);
        var userCancellationToken = cancellationRegistry.Register(transferIdentifier);

        try
        {
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                userCancellationToken
            );

            try
            {
                await ChangeArchiveFileHashesAsync(
                    archive: archive,
                    archiveFiles: archiveFiles,
                    archiveConfig: archiveConfig,
                    knownHashes: await LoadKnownHashesAsync(
                        archiveConfig.Id,
                        linkedTokenSource.Token
                    ),
                    transferType: TransferType.ArchiveHashChange,
                    cancellationToken: linkedTokenSource.Token
                );

                return true;
            }
            catch (OperationCanceledException) when (userCancellationToken.IsCancellationRequested)
            {
                await SaveChangedHashesAndCancelWaitingUploadsAsync(
                    archive: archive,
                    waitingUploads: waitingUploads,
                    cancellationToken: cancellationToken
                );

                return false;
            }
            catch (OperationCanceledException)
            {
                await repository.SaveChangesAsync(cancellationToken: CancellationToken.None);

                throw;
            }
        }
        finally
        {
            cancellationRegistry.Unregister(transferIdentifier);
        }
    }

    private async Task SaveChangedHashesAndCancelWaitingUploadsAsync(
        Archive archive,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Canceled the MD5 hash change of existing archive {ArchiveId} on user request. {FileCountWithoutHash} archive files have no valid hash anymore and get new hashes before the archive is assigned again",
            archive.Id,
            archive.ArchiveFiles.Count(f => f.Md5Hash is null)
        );

        CancelUploadsBecauseArchiveCreationWasCanceled(waitingUploads);

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }

    private void CancelUploadsBecauseArchiveCreationWasCanceled(
        IReadOnlyList<Upload> waitingUploads
    )
    {
        foreach (var upload in waitingUploads)
        {
            upload.UploadState = UploadState.Canceled;

            notificationService.Create(
                kind: NotificationKind.UploadCanceled,
                message: "Upload canceled because the archive creation was canceled.",
                entity: upload,
                selector: n => n.Upload
            );
        }

        logger.LogInformation(
            "Canceled {UploadCount} uploads that were waiting for the canceled archive creation",
            waitingUploads.Count
        );
    }

    private void CarryOverOnlineFiles(Upload newUpload, Archive assignableArchive)
    {
        var reusableOnlineFiles = ArchiveFilesToUpload.GetOnlineUploadedFilesToCarryOver(
            upload: newUpload,
            uploadsOfArchive: assignableArchive.Uploads
        );

        if (reusableOnlineFiles.Count == 0)
        {
            return;
        }

        newUpload.UploadedFiles = reusableOnlineFiles
            .Select(source => new UploadedFile
            {
                Upload = newUpload,
                ArchiveFileId = source.ArchiveFileId,
                HosterFileLink = source.HosterFileLink,
                ExternalId = source.ExternalId,
                HosterFolderId = source.HosterFolderId,
                Md5Hash = source.Md5Hash,
                OnlineState = OnlineState.Online,
                CreatedAt = source.CreatedAt,
                CheckedAt = source.CheckedAt,
            })
            .ToList();

        logger.LogInformation(
            "Carried over {FileCount} online files to reupload {UploadId} from previous uploads of archive {ArchiveId}",
            newUpload.UploadedFiles.Count,
            newUpload.Id,
            assignableArchive.Id
        );
    }

    private async Task<bool> ArchiveNeedsHashChangeAsync(
        ArchiveConfig archiveConfig,
        IReadOnlyList<Upload> uploads,
        CancellationToken cancellationToken
    )
    {
        foreach (
            var hosterClassName in uploads
                .Select(u => u.UploadConfig.HosterRegistration.HosterClassName)
                .Distinct(StringComparer.Ordinal)
        )
        {
            if (
                await repository.HasCompletedUploadForHosterAsync(
                    archiveConfigId: archiveConfig.Id,
                    hosterClassName: hosterClassName,
                    cancellationToken: cancellationToken
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private async Task<HashSet<string>> LoadKnownHashesAsync(
        int archiveConfigId,
        CancellationToken cancellationToken
    )
    {
        var hashes = await repository.GetKnownArchiveFileHashesAsync(
            archiveConfigId: archiveConfigId,
            cancellationToken: cancellationToken
        );

        return hashes.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task ChangeArchiveFileHashesAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> archiveFiles,
        ArchiveConfig archiveConfig,
        HashSet<string> knownHashes,
        TransferType transferType,
        CancellationToken cancellationToken
    )
    {
        await HashArchiveFilesInParallelWithProgressAsync(
            archive: archive,
            archiveFiles: archiveFiles,
            archiveConfig: archiveConfig,
            transferType: transferType,
            hashArchiveFileAsync: async (archiveFile, progress, fileCancellationToken) =>
            {
                if (!File.Exists(archiveFile.FullFileName))
                {
                    logger.LogWarning(
                        "Cannot change MD5 hash for missing archive file {ArchiveFileName} in archive {ArchiveId}",
                        archiveFile.FullFileName,
                        archive.Id
                    );

                    return;
                }

                archiveFile.Md5Hash = null;

                progress.BeginFile(new FileInfo(archiveFile.FullFileName).Length);

                var nullByteSuffixHash =
                    await Md5FileHash.ComputeHashWithShortestUnknownNullByteSuffixAsync(
                        fullFileName: archiveFile.FullFileName,
                        addHashIfUnknown: hash =>
                        {
                            lock (knownHashesLock)
                            {
                                return knownHashes.Add(hash);
                            }
                        },
                        progress: progress,
                        cancellationToken: fileCancellationToken
                    );

                await AppendNullBytesAsync(
                    fullFileName: archiveFile.FullFileName,
                    nullByteCount: nullByteSuffixHash.NullByteCount,
                    cancellationToken: fileCancellationToken
                );

                archiveFile.Md5Hash = nullByteSuffixHash.Md5Hash;
            },
            cancellationToken: cancellationToken
        );

        logger.LogInformation(
            "Changed MD5 hash for {FileCount} archive files in archive {ArchiveId}",
            archiveFiles.Count,
            archive.Id
        );
    }

    private async Task StoreArchiveFileHashesAsync(
        Archive archive,
        ArchiveConfig archiveConfig,
        CancellationToken cancellationToken
    )
    {
        await HashArchiveFilesInParallelWithProgressAsync(
            archive: archive,
            archiveFiles: archive.ArchiveFiles,
            archiveConfig: archiveConfig,
            transferType: TransferType.ArchiveHashing,
            hashArchiveFileAsync: async (archiveFile, progress, fileCancellationToken) =>
            {
                if (!File.Exists(archiveFile.FullFileName))
                {
                    logger.LogWarning(
                        "Cannot compute MD5 hash for missing archive file {ArchiveFileName} in archive {ArchiveId}",
                        archiveFile.FullFileName,
                        archive.Id
                    );

                    return;
                }

                archiveFile.Md5Hash = await ComputeMd5HashAsync(
                    archiveFile.FullFileName,
                    progress,
                    fileCancellationToken
                );
            },
            cancellationToken: cancellationToken
        );
    }

    private async Task HashArchiveFilesInParallelWithProgressAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> archiveFiles,
        ArchiveConfig archiveConfig,
        TransferType transferType,
        Func<ArchiveFile, ITransferProgress, CancellationToken, Task> hashArchiveFileAsync,
        CancellationToken cancellationToken
    )
    {
        var transferIdentifier = new TransferIdentifier(transferType, archive.Id);

        var plannedFilesPerArchiveFile = archiveFiles
            .Select(
                (archiveFile, index) =>
                    (
                        ArchiveFile: archiveFile,
                        PlannedFile: new TransferFile(
                            FileId: index + 1,
                            FileName: Path.GetFileName(archiveFile.FullFileName),
                            SourceName: archiveConfig.Name,
                            SizeBytes: GetFileSizeBytesOrZeroWhenMissing(archiveFile.FullFileName),
                            IsAlreadyTransferred: false
                        )
                    )
            )
            .ToList();

        progressTracker.StartTracking(
            transferIdentifier,
            plannedFilesPerArchiveFile.Select(entry => entry.PlannedFile).ToList()
        );

        try
        {
            await Parallel.ForEachAsync(
                plannedFilesPerArchiveFile,
                CreateHashingParallelOptions(cancellationToken),
                async (entry, fileCancellationToken) =>
                    await hashArchiveFileAsync(
                        entry.ArchiveFile,
                        new TransferProgressReporter(
                            tracker: progressTracker,
                            identifier: transferIdentifier,
                            fileId: entry.PlannedFile.FileId,
                            fileName: entry.PlannedFile.FileName,
                            sourceName: entry.PlannedFile.SourceName
                        ),
                        fileCancellationToken
                    )
            );
        }
        finally
        {
            progressTracker.StopTracking(transferIdentifier);
        }
    }

    private static long GetFileSizeBytesOrZeroWhenMissing(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        return fileInfo.Exists ? fileInfo.Length : 0;
    }

    private static async Task<string> ComputeMd5HashAsync(
        string fullFileName,
        ITransferProgress progress,
        CancellationToken cancellationToken
    )
    {
        progress.BeginFile(new FileInfo(fullFileName).Length);

        return await Md5FileHash.ComputeAsync(fullFileName, progress, cancellationToken);
    }

    private ParallelOptions CreateHashingParallelOptions(CancellationToken cancellationToken)
    {
        return new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(
                1,
                configurationProvider.GetValue<ArchiveRepackagingConfiguration>(c =>
                    c.MaxParallelHashOperations
                )
            ),
            CancellationToken = cancellationToken,
        };
    }

    private static async Task AppendNullBytesAsync(
        string fullFileName,
        int nullByteCount,
        CancellationToken cancellationToken
    )
    {
        await using var stream = new FileStream(
            path: fullFileName,
            mode: FileMode.Append,
            access: FileAccess.Write,
            share: FileShare.Read
        );
        await stream.WriteAsync(new byte[nullByteCount], cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private async Task CreateArchiveAsync(
        ArchiveConfig config,
        IReadOnlyList<Upload> uploads,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Creating archive for ArchiveConfig {ArchiveConfigId} with {UploadCount} uploads and archiver {ArchiverClassName}",
            config.Id,
            uploads.Count,
            config.ArchiverName
        );

        var releaseFolderPath = config.Release.ReleaseFolderPath;

        if (string.IsNullOrEmpty(releaseFolderPath) || !Directory.Exists(releaseFolderPath))
        {
            logger.LogError(
                "Release folder path {ReleaseFolderPath} does not exist for ArchiveConfig {ArchiveConfigId}",
                releaseFolderPath,
                config.Id
            );

            foreach (var upload in uploads)
            {
                upload.UploadState = UploadState.Failed;
                notificationService.Create(
                    kind: NotificationKind.ReleaseFolderMissing,
                    message: $"Release folder path {releaseFolderPath} does not exist.",
                    entity: upload,
                    selector: n => n.Upload
                );
            }
            await repository.SaveChangesAsync(cancellationToken: cancellationToken);
            return;
        }

        if (!fileSystemService.DirectoryExists(config.ArchiveFilesBasePath))
        {
            logger.LogError(
                "Archive folder {ArchiveFilesBasePath} does not exist for ArchiveConfig {ArchiveConfigId}",
                config.ArchiveFilesBasePath,
                config.Id
            );

            foreach (var upload in uploads)
            {
                upload.UploadState = UploadState.Failed;
                notificationService.Create(
                    kind: NotificationKind.ArchiveCreationFailed,
                    message: $"Archive folder {config.ArchiveFilesBasePath} does not exist. Bearcat does not create it, so check that it exists or is mounted.",
                    entity: upload,
                    selector: n => n.Upload
                );
            }
            await repository.SaveChangesAsync(cancellationToken: cancellationToken);
            return;
        }

        var archiver = archiverFactory.GetByName(config.ArchiverName);
        var archiveDirectoryPath = fileSystemService.CreateTempDirectory(
            config.ArchiveFilesBasePath
        );

        var lastArchiveHasUnknownHashes = await repository.LastArchiveHasFilesWithoutHashAsync(
            archiveConfigId: config.Id,
            cancellationToken: cancellationToken
        );

        var useHashAppendStrategy = archiver.CanChangeHashInPlace && !lastArchiveHasUnknownHashes;

        var archiveSettings = await ResolveArchiveSettingsAsync(
            config: config,
            useHashAppendStrategy: useHashAppendStrategy,
            archiverCanChangeHashInPlace: archiver.CanChangeHashInPlace,
            cancellationToken: cancellationToken
        );

        var archive = new Archive
        {
            ArchiveConfig = config,
            ArchiveFolderPath = archiveDirectoryPath,
            ArchiveFiles = [],
            ArchiveState = ArchiveState.Creating,
            ArchiveFileSizeMb = archiveSettings.ArchiveFileSizeMb,
            CreatedAt = timeProvider.GetLocalNow(),
            Uploads = uploads.ToList(),
            ErrorMessages = [],
        };

        repository.Add(archive);
        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        var transferIdentifier = new TransferIdentifier(TransferType.ArchiveCreation, archive.Id);
        var userCancellationToken = cancellationRegistry.Register(transferIdentifier);

        try
        {
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                userCancellationToken
            );

            try
            {
                await PackAndHashArchiveAsync(
                    archive: archive,
                    archiver: archiver,
                    releaseFolderPath: releaseFolderPath,
                    archiveSettings: archiveSettings,
                    cancellationToken: linkedTokenSource.Token
                );
            }
            catch (OperationCanceledException) when (userCancellationToken.IsCancellationRequested)
            {
                await DeleteArchiveAndCancelWaitingUploadsAsync(
                    archive: archive,
                    waitingUploads: uploads,
                    cancellationToken: cancellationToken
                );
            }
        }
        finally
        {
            cancellationRegistry.Unregister(transferIdentifier);
        }
    }

    private async Task<ArchiveResult> CreateArchiveAsync(
        Archive archive,
        IArchiver archiver,
        string releaseFolderPath,
        IReadOnlyList<ReleaseFolderEntryForPacking> entriesForPacking,
        ArchiveSettings archiveSettings,
        CancellationToken cancellationToken
    )
    {
        archive.ReleaseFolderEntriesCopiedForPacking = entriesForPacking
            .Select(entry => entry.Name)
            .ToList();
        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        try
        {
            var copyErrorMessage =
                await releaseFolderEntriesForPackingService.CopyIntoReleaseFolderAsync(
                    releaseFolderPath: releaseFolderPath,
                    entries: entriesForPacking,
                    cancellationToken: cancellationToken
                );

            if (copyErrorMessage is not null)
            {
                return new ArchiveResult(
                    IsSuccess: false,
                    CreatedFileNames: [],
                    ErrorMessages: [copyErrorMessage]
                );
            }

            // For people that host Bearcat on a Synology NAS: DSM adds that nasty hidden @eaDir folder everywhere where media is.
            // So we should remove it before archiving.
            RemoveSynologyMetadataFolders(releaseFolderPath);

            return await PackReleaseFolderWithProgressAsync(
                archive: archive,
                archiver: archiver,
                releaseFolderPath: releaseFolderPath,
                archiveSettings: archiveSettings,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            releaseFolderEntriesForPackingService.DeleteFromReleaseFolder(
                releaseFolderPath,
                archive.ReleaseFolderEntriesCopiedForPacking
            );
            archive.ReleaseFolderEntriesCopiedForPacking = [];
            await repository.SaveChangesAsync(cancellationToken: CancellationToken.None);
        }
    }

    private async Task DeleteArchiveAndCancelWaitingUploadsAsync(
        Archive archive,
        IReadOnlyList<Upload> waitingUploads,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Canceled the creation of archive {ArchiveId} on user request, deleting the archive and its folder {ArchiveFolderPath}",
            archive.Id,
            archive.ArchiveFolderPath
        );

        DeleteArchiveFolderAndRemoveArchive(archive);
        CancelUploadsBecauseArchiveCreationWasCanceled(waitingUploads);

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }

    private async Task PackAndHashArchiveAsync(
        Archive archive,
        IArchiver archiver,
        string releaseFolderPath,
        ArchiveSettings archiveSettings,
        CancellationToken cancellationToken
    )
    {
        var config = archive.ArchiveConfig;

        var entriesForPacking =
            releaseFolderEntriesForPackingService.GetReleaseFolderEntriesForPacking(
                releaseFolderPath: releaseFolderPath,
                additionalArchiveContents: config.AdditionalArchiveContents,
                createNonceFile: config.CreateNonceFile
            );

        if (entriesForPacking.ErrorMessages.Count > 0)
        {
            await MarkArchiveCreationFailedAsync(
                archive,
                entriesForPacking.ErrorMessages,
                cancellationToken
            );

            return;
        }

        var archiveResult = await CreateArchiveAsync(
            archive: archive,
            archiver: archiver,
            releaseFolderPath: releaseFolderPath,
            entriesForPacking: entriesForPacking.Entries,
            archiveSettings: archiveSettings,
            cancellationToken: cancellationToken
        );

        if (!archiveResult.IsSuccess)
        {
            await MarkArchiveCreationFailedAsync(
                archive,
                archiveResult.ErrorMessages ?? [],
                cancellationToken
            );

            return;
        }

        archive.ArchiveFiles = archiveResult
            .CreatedFileNames.Select(f => new ArchiveFile { FullFileName = f })
            .ToList();

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        await HashArchiveFilesAsync(archive, config, archiver, cancellationToken);
        FinalizeArchive(archive);

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);

        logger.LogInformation(
            "Created archive {ArchiveId} for ArchiveConfig {ArchiveConfigId} with {FileCount} files",
            archive.Id,
            config.Id,
            archive.ArchiveFiles.Count
        );
    }

    private async Task<ArchiveResult> PackReleaseFolderWithProgressAsync(
        Archive archive,
        IArchiver archiver,
        string releaseFolderPath,
        ArchiveSettings archiveSettings,
        CancellationToken cancellationToken
    )
    {
        var transferIdentifier = new TransferIdentifier(TransferType.ArchiveCreation, archive.Id);

        var packedReleaseFolder = new TransferFile(
            FileId: 1,
            FileName: archive.ArchiveConfig.Release.Name,
            SourceName: archive.ArchiveConfig.Name,
            SizeBytes: fileSystemService.GetFolderFileCountAndSize(releaseFolderPath).TotalBytes,
            IsAlreadyTransferred: false
        );

        progressTracker.StartTracking(transferIdentifier, [packedReleaseFolder]);

        try
        {
            return await folderSizeProgressReporter.RunWhileReportingFolderSizeAsync(
                folderPath: archive.ArchiveFolderPath,
                progress: new TransferProgressReporter(
                    tracker: progressTracker,
                    identifier: transferIdentifier,
                    fileId: packedReleaseFolder.FileId,
                    fileName: packedReleaseFolder.FileName,
                    sourceName: packedReleaseFolder.SourceName
                ),
                operation: () =>
                    archiver.ArchiveAsync(
                        sourceFolderPath: releaseFolderPath,
                        destinationPath: archive.ArchiveFolderPath,
                        archiveNamePrefix: archive.ArchiveConfig.ArchiveNamePrefix
                            ?? Guid.NewGuid().ToString(),
                        targetFileSizeMb: archiveSettings.ArchiveFileSizeMb,
                        password: archive.ArchiveConfig.ArchivePassword,
                        options: archiveSettings.Options,
                        cancellationToken: cancellationToken
                    ),
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            progressTracker.StopTracking(transferIdentifier);
        }
    }

    private async Task MarkArchiveCreationFailedAsync(
        Archive archive,
        IReadOnlyList<string> errorMessages,
        CancellationToken cancellationToken
    )
    {
        logger.LogError(
            "Failed to create archive for ArchiveConfig {ArchiveConfigId}: {ErrorMessages}",
            archive.ArchiveConfig.Id,
            string.Join(",  ", errorMessages)
        );

        archive.ArchiveState = ArchiveState.CreationFailed;
        archive.ErrorMessages.AddRange(errorMessages);

        notificationService.Create(
            kind: NotificationKind.ArchiveCreationFailed,
            message: $"Failed to create archive: {string.Join(", ", errorMessages)}",
            entity: archive,
            selector: n => n.Archive
        );

        await repository.SaveChangesAsync(cancellationToken: cancellationToken);
    }

    private void RemoveSynologyMetadataFolders(string releasePath)
    {
        var deletedFolders = fileSystemService.DeleteDirectoriesByNameRecursively(
            rootPath: releasePath,
            directoryName: SynologyMetadataFolderName
        );

        if (deletedFolders.Count == 0)
        {
            return;
        }

        logger.LogInformation(
            "Removed {FolderCount} Synology metadata folders ({FolderName}) from release folder {ReleaseFolderPath} before archiving",
            deletedFolders.Count,
            SynologyMetadataFolderName,
            releasePath
        );
    }

    private async Task<ArchiveSettings> ResolveArchiveSettingsAsync(
        ArchiveConfig config,
        bool useHashAppendStrategy,
        bool archiverCanChangeHashInPlace,
        CancellationToken cancellationToken
    )
    {
        var strategy = configurationProvider.GetValue<ArchiveRepackagingConfiguration>(c =>
            c.Strategy
        );

        var uncompressedArchiveOptions = new ArchiveOptions(
            UseCompression: false,
            UseSolidArchive: false,
            PackSourceFolderAsRootFolder: config.PackReleaseFolderAsRootFolder
        );

        var archiveFileSizeMustChangeForUniqueHashes =
            !config.CreateNonceFile && !archiverCanChangeHashInPlace;

        if (
            string.Equals(
                strategy,
                ArchiveRepackagingStrategies.NonceOnly,
                StringComparison.Ordinal
            )
        )
        {
            return new ArchiveSettings(
                ArchiveFileSizeMb: await GetArchiveFileSizeMbAsync(
                    config: config,
                    incrementLastArchiveFileSize: archiveFileSizeMustChangeForUniqueHashes,
                    cancellationToken: cancellationToken
                ),
                Options: uncompressedArchiveOptions
            );
        }

        if (
            string.Equals(
                strategy,
                ArchiveRepackagingStrategies.SolidCompression,
                StringComparison.Ordinal
            )
        )
        {
            return new ArchiveSettings(
                ArchiveFileSizeMb: await GetArchiveFileSizeMbAsync(
                    config: config,
                    incrementLastArchiveFileSize: archiveFileSizeMustChangeForUniqueHashes,
                    cancellationToken: cancellationToken
                ),
                Options: new ArchiveOptions(
                    UseCompression: true,
                    UseSolidArchive: true,
                    PackSourceFolderAsRootFolder: config.PackReleaseFolderAsRootFolder
                )
            );
        }

        if (
            !string.Equals(
                strategy,
                ArchiveRepackagingStrategies.IncrementArchiveFileSize,
                StringComparison.Ordinal
            )
        )
        {
            logger.LogWarning(
                "Unknown archive repackaging strategy {Strategy}. Falling back to {DefaultStrategy}.",
                strategy,
                ArchiveRepackagingStrategies.IncrementArchiveFileSize
            );
        }

        return new ArchiveSettings(
            ArchiveFileSizeMb: await GetArchiveFileSizeMbAsync(
                config: config,
                incrementLastArchiveFileSize: !useHashAppendStrategy,
                cancellationToken: cancellationToken
            ),
            Options: uncompressedArchiveOptions
        );
    }

    private async Task<int> GetArchiveFileSizeMbAsync(
        ArchiveConfig config,
        bool incrementLastArchiveFileSize,
        CancellationToken cancellationToken
    )
    {
        if (!incrementLastArchiveFileSize)
        {
            return config.ArchiveFileSizeMb;
        }

        var lastArchiveFileSizeMb = await repository.GetLastArchiveFileSizeMbAsync(
            archiveConfigId: config.Id,
            cancellationToken: cancellationToken
        );

        return lastArchiveFileSizeMb is null
            ? config.ArchiveFileSizeMb
            : lastArchiveFileSizeMb.Value + 1;
    }

    private sealed record ArchiveSettings(int ArchiveFileSizeMb, ArchiveOptions Options);
}
