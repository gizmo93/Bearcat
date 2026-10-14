using System.Globalization;
using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ArchiveFileCopies;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.StorageFolderMoves;

public class ArchiveStorageFolderMoveService(
    IArchiveCleanupRepository repository,
    IFileSystemService fileSystemService,
    INotificationService notificationService,
    ArchiveFileCopyService archiveFileCopyService,
    ITransferCancellationRegistry cancellationRegistry,
    ILogger<ArchiveStorageFolderMoveService> logger
)
{
    public async Task MoveArchivesAsync(
        IReadOnlyList<Archive> archives,
        CancellationToken cancellationToken
    )
    {
        if (archives.Count == 0)
        {
            return;
        }

        var storageFolders = await repository.GetActiveArchiveStorageFoldersAsync(
            cancellationToken
        );

        var archiveIdsWithUnresolvedNotification = new ArchiveIdsWithUnresolvedNotification(
            NoStorageFolderAvailable: await GetArchiveIdsWithUnresolvedNotificationAsync(
                NotificationKind.NoArchiveStorageFolderAvailable,
                cancellationToken
            ),
            MoveFailed: await GetArchiveIdsWithUnresolvedNotificationAsync(
                NotificationKind.ArchiveMoveToStorageFolderFailed,
                cancellationToken
            )
        );

        foreach (var archive in archives)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await MoveArchiveAsync(
                archive: archive,
                storageFolders: storageFolders,
                archiveIdsWithUnresolvedNotification: archiveIdsWithUnresolvedNotification,
                cancellationToken: cancellationToken
            );
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<HashSet<int>> GetArchiveIdsWithUnresolvedNotificationAsync(
        NotificationKind notificationKind,
        CancellationToken cancellationToken
    )
    {
        var archiveIds = await repository.GetArchiveIdsWithUnresolvedNotificationAsync(
            notificationKind,
            cancellationToken
        );

        return archiveIds.ToHashSet();
    }

    private async Task MoveArchiveAsync(
        Archive archive,
        IReadOnlyList<ArchiveStorageFolder> storageFolders,
        ArchiveIdsWithUnresolvedNotification archiveIdsWithUnresolvedNotification,
        CancellationToken cancellationToken
    )
    {
        var transferIdentifier = new TransferIdentifier(
            TransferType.ArchiveMoveToStorageFolder,
            archive.Id
        );

        using var userCancellableTransfer = cancellationRegistry.RegisterUserCancellableTransfer(
            transferIdentifier,
            cancellationToken
        );

        var copiedArchive = await CopyArchiveIntoStorageFolderAsync(
            archive: archive,
            storageFolders: storageFolders,
            archiveIdsWithUnresolvedNotification: archiveIdsWithUnresolvedNotification,
            transferIdentifier: transferIdentifier,
            userCancellableTransfer: userCancellableTransfer,
            cancellationToken: cancellationToken
        );

        if (copiedArchive is null)
        {
            return;
        }

        await PointArchiveToStorageFolderAndDeleteLocalFilesAsync(
            archive: archive,
            copiedArchive: copiedArchive,
            cancellationToken: cancellationToken
        );
    }

    private async Task<ArchiveCopiedIntoStorageFolder?> CopyArchiveIntoStorageFolderAsync(
        Archive archive,
        IReadOnlyList<ArchiveStorageFolder> storageFolders,
        ArchiveIdsWithUnresolvedNotification archiveIdsWithUnresolvedNotification,
        TransferIdentifier transferIdentifier,
        UserCancellableTransfer userCancellableTransfer,
        CancellationToken cancellationToken
    )
    {
        ArchiveStorageFolder? storageFolder = null;

        try
        {
            var archiveFilesWithSize = archiveFileCopyService.GetArchiveFilesWithSize(
                archive.ArchiveFiles.OrderBy(file => file.Id).ToList()
            );
            var archiveSizeBytes = archiveFilesWithSize.Sum(file => file.SizeBytes);

            storageFolder = ArchiveStorageFolderSelector.SelectStorageFolder(
                storageFolders: GetStorageFoldersWithAvailableFreeSpace(storageFolders),
                archiveSizeBytes: archiveSizeBytes
            );

            if (storageFolder is null)
            {
                ReportNoStorageFolderAvailable(
                    archive: archive,
                    archiveSizeBytes: archiveSizeBytes,
                    archiveIdsWithUnresolvedNotification: archiveIdsWithUnresolvedNotification.NoStorageFolderAvailable
                );

                return null;
            }

            if (!fileSystemService.DirectoryExists(storageFolder.Path))
            {
                throw new DirectoryNotFoundException(
                    $"Storage folder path {storageFolder.Path} does not exist"
                );
            }

            var targetFolderPath = GetTargetFolderPath(storageFolder, archive);

            var copiedFiles = await archiveFileCopyService.CopyArchiveFilesAndVerifySizesAsync(
                archiveId: archive.Id,
                archiveFilesWithSize: archiveFilesWithSize,
                targetFolderPath: targetFolderPath,
                sourceName: storageFolder.Name,
                transferIdentifier: transferIdentifier,
                cancellationToken: userCancellableTransfer.CancellationToken
            );

            return new ArchiveCopiedIntoStorageFolder(
                StorageFolder: storageFolder,
                TargetFolderPath: targetFolderPath,
                ArchiveFilesWithSize: archiveFilesWithSize,
                CopiedFiles: copiedFiles
            );
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException && userCancellableTransfer.WasCanceledByUser)
            {
                logger.LogInformation(
                    "Canceled moving archive {ArchiveId} to a storage folder on user request, the archive stays at {ArchiveFolderPath}",
                    archive.Id,
                    archive.ArchiveFolderPath
                );

                return null;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            ReportMoveFailed(
                archive: archive,
                storageFolder: storageFolder,
                exception: ex,
                archiveIdsWithUnresolvedNotification: archiveIdsWithUnresolvedNotification.MoveFailed
            );

            return null;
        }
    }

    private List<ArchiveStorageFolderWithAvailableFreeSpace> GetStorageFoldersWithAvailableFreeSpace(
        IReadOnlyList<ArchiveStorageFolder> storageFolders
    )
    {
        return storageFolders
            .Select(storageFolder => new ArchiveStorageFolderWithAvailableFreeSpace(
                storageFolder,
                fileSystemService.GetAvailableFreeSpaceBytes(storageFolder.Path)
            ))
            .ToList();
    }

    private string GetTargetFolderPath(ArchiveStorageFolder storageFolder, Archive archive)
    {
        var archiveFolderName = Path.GetFileName(
            Path.TrimEndingDirectorySeparator(archive.ArchiveFolderPath)
        );

        var targetFolderPath = Path.Join(storageFolder.Path, archiveFolderName);

        if (!fileSystemService.DirectoryExists(targetFolderPath))
        {
            return targetFolderPath;
        }

        return Path.Join(storageFolder.Path, $"{archiveFolderName}.{archive.Id}");
    }

    private async Task PointArchiveToStorageFolderAndDeleteLocalFilesAsync(
        Archive archive,
        ArchiveCopiedIntoStorageFolder copiedArchive,
        CancellationToken cancellationToken
    )
    {
        if (await ArchiveChangedDuringCopyAsync(archive, copiedArchive, cancellationToken))
        {
            archiveFileCopyService.DeleteCopiedFiles(
                archiveId: archive.Id,
                copiedFiles: copiedArchive.CopiedFiles,
                targetFolderPath: copiedArchive.TargetFolderPath
            );

            return;
        }

        var localArchiveFolderPath = archive.ArchiveFolderPath;
        var localFilePaths = archive.ArchiveFiles.Select(file => file.FullFileName).ToList();

        foreach (var copiedFile in copiedArchive.CopiedFiles)
        {
            copiedFile.ArchiveFile.FullFileName = copiedFile.CopiedFilePath;
            copiedFile.ArchiveFile.Md5HashInStorageFolder = copiedFile.ArchiveFile.Md5Hash;
        }

        archive.ArchiveFolderPath = copiedArchive.TargetFolderPath;
        archive.ArchiveStorageFolderId = copiedArchive.StorageFolder.Id;
        archive.ArchiveStorageFolder = copiedArchive.StorageFolder;

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Moved archive {ArchiveId} from {LocalArchiveFolderPath} to storage folder {StorageFolderName} at {TargetFolderPath}",
            archive.Id,
            localArchiveFolderPath,
            copiedArchive.StorageFolder.Name,
            copiedArchive.TargetFolderPath
        );

        try
        {
            foreach (var localFilePath in localFilePaths)
            {
                fileSystemService.DeleteFileIfExists(localFilePath);
            }

            fileSystemService.DeleteDirectoryIfEmpty(localArchiveFolderPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Moved archive {ArchiveId} to {TargetFolderPath} but could not delete the local files in {LocalArchiveFolderPath}",
                archive.Id,
                copiedArchive.TargetFolderPath,
                localArchiveFolderPath
            );
        }
    }

    private async Task<bool> ArchiveChangedDuringCopyAsync(
        Archive archive,
        ArchiveCopiedIntoStorageFolder copiedArchive,
        CancellationToken cancellationToken
    )
    {
        if (
            await repository.HasWaitingPendingOrUploadingUploadAsync(
                archive.ArchiveConfigId,
                cancellationToken
            )
        )
        {
            logger.LogInformation(
                "Archive {ArchiveId} stays at {ArchiveFolderPath} because an upload of its archive config started while it was copied to a storage folder, retrying in the next run",
                archive.Id,
                archive.ArchiveFolderPath
            );

            return true;
        }

        var changedFile = copiedArchive.ArchiveFilesWithSize.FirstOrDefault(file =>
            !fileSystemService.FileExists(file.ArchiveFile.FullFileName)
            || fileSystemService.GetFileSizeBytes(file.ArchiveFile.FullFileName) != file.SizeBytes
        );

        if (changedFile is null)
        {
            return false;
        }

        logger.LogInformation(
            "Archive {ArchiveId} stays at {ArchiveFolderPath} because {FilePath} changed while it was copied to a storage folder, retrying in the next run",
            archive.Id,
            archive.ArchiveFolderPath,
            changedFile.ArchiveFile.FullFileName
        );

        return true;
    }

    private void ReportNoStorageFolderAvailable(
        Archive archive,
        long archiveSizeBytes,
        HashSet<int> archiveIdsWithUnresolvedNotification
    )
    {
        var archiveSize = archiveSizeBytes.Bytes().Humanize("0.0", CultureInfo.InvariantCulture);

        logger.LogWarning(
            "Archive {ArchiveId} ({ArchiveSize}) stays at {ArchiveFolderPath} because no active storage folder has enough free space",
            archive.Id,
            archiveSize,
            archive.ArchiveFolderPath
        );

        if (!archiveIdsWithUnresolvedNotification.Add(archive.Id))
        {
            return;
        }

        notificationService.Create(
            kind: NotificationKind.NoArchiveStorageFolderAvailable,
            message: $"Archive {archive.Id} of release '{archive.ArchiveConfig.Release.Name}' ({archiveSize}) could not be moved: no active storage folder has enough free space for it plus its minimum free space. The archive stays at {archive.ArchiveFolderPath}.",
            entity: archive,
            selector: n => n.Archive
        );
    }

    private void ReportMoveFailed(
        Archive archive,
        ArchiveStorageFolder? storageFolder,
        Exception exception,
        HashSet<int> archiveIdsWithUnresolvedNotification
    )
    {
        logger.LogWarning(
            exception,
            "Could not move archive {ArchiveId} to storage folder {StorageFolderName}, the archive stays at {ArchiveFolderPath}",
            archive.Id,
            storageFolder?.Name,
            archive.ArchiveFolderPath
        );

        if (!archiveIdsWithUnresolvedNotification.Add(archive.Id))
        {
            return;
        }

        var storageFolderText = storageFolder is null
            ? "a storage folder"
            : $"storage folder '{storageFolder.Name}'";

        notificationService.Create(
            kind: NotificationKind.ArchiveMoveToStorageFolderFailed,
            message: $"Archive {archive.Id} of release '{archive.ArchiveConfig.Release.Name}' could not be moved to {storageFolderText}: {exception.Message.TrimEnd('.')}. The archive stays at {archive.ArchiveFolderPath}.",
            entity: archive,
            selector: n => n.Archive
        );
    }

    private sealed record ArchiveCopiedIntoStorageFolder(
        ArchiveStorageFolder StorageFolder,
        string TargetFolderPath,
        IReadOnlyList<ArchiveFileWithSize> ArchiveFilesWithSize,
        IReadOnlyList<CopiedArchiveFile> CopiedFiles
    );

    private sealed record ArchiveIdsWithUnresolvedNotification(
        HashSet<int> NoStorageFolderAvailable,
        HashSet<int> MoveFailed
    );
}
