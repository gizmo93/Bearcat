using System.Globalization;
using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.StorageFolderMoves;

public class ArchiveStorageFolderMoveService(
    IArchiveCleanupRepository repository,
    IFileSystemService fileSystemService,
    INotificationService notificationService,
    ITransferProgressTracker progressTracker,
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

        var userCancellationToken = cancellationRegistry.Register(transferIdentifier);

        try
        {
            var copiedArchive = await CopyArchiveIntoStorageFolderAsync(
                archive: archive,
                storageFolders: storageFolders,
                archiveIdsWithUnresolvedNotification: archiveIdsWithUnresolvedNotification,
                transferIdentifier: transferIdentifier,
                userCancellationToken: userCancellationToken,
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
        finally
        {
            cancellationRegistry.Unregister(transferIdentifier);
        }
    }

    private async Task<ArchiveCopiedIntoStorageFolder?> CopyArchiveIntoStorageFolderAsync(
        Archive archive,
        IReadOnlyList<ArchiveStorageFolder> storageFolders,
        ArchiveIdsWithUnresolvedNotification archiveIdsWithUnresolvedNotification,
        TransferIdentifier transferIdentifier,
        CancellationToken userCancellationToken,
        CancellationToken cancellationToken
    )
    {
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            userCancellationToken
        );

        var copiedFilePaths = new List<string>();
        ArchiveStorageFolder? storageFolder = null;
        string? targetFolderPath = null;

        try
        {
            var archiveFilesWithSize = archive
                .ArchiveFiles.OrderBy(file => file.Id)
                .Select(file => new ArchiveFileWithSize(
                    file,
                    fileSystemService.GetFileSizeBytes(file.FullFileName)
                ))
                .ToList();
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

            targetFolderPath = GetTargetFolderPath(storageFolder, archive);

            await CopyAndVerifyArchiveFilesAsync(
                archiveFilesWithSize: archiveFilesWithSize,
                storageFolder: storageFolder,
                targetFolderPath: targetFolderPath,
                copiedFilePaths: copiedFilePaths,
                transferIdentifier: transferIdentifier,
                cancellationToken: linkedTokenSource.Token
            );

            return new ArchiveCopiedIntoStorageFolder(
                StorageFolder: storageFolder,
                TargetFolderPath: targetFolderPath,
                ArchiveFilesWithSize: archiveFilesWithSize,
                CopiedFilePaths: copiedFilePaths
            );
        }
        catch (Exception ex)
        {
            DeleteCopiedFiles(archive, copiedFilePaths, targetFolderPath);

            if (ex is OperationCanceledException && userCancellationToken.IsCancellationRequested)
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

    private async Task CopyAndVerifyArchiveFilesAsync(
        IReadOnlyList<ArchiveFileWithSize> archiveFilesWithSize,
        ArchiveStorageFolder storageFolder,
        string targetFolderPath,
        List<string> copiedFilePaths,
        TransferIdentifier transferIdentifier,
        CancellationToken cancellationToken
    )
    {
        if (!fileSystemService.DirectoryExists(storageFolder.Path))
        {
            throw new DirectoryNotFoundException(
                $"Storage folder path {storageFolder.Path} does not exist"
            );
        }

        fileSystemService.CreateDirectory(targetFolderPath);

        progressTracker.StartTracking(
            identifier: transferIdentifier,
            plannedFiles: archiveFilesWithSize
                .Select(file => new TransferFile(
                    FileId: file.ArchiveFile.Id,
                    FileName: Path.GetFileName(file.ArchiveFile.FullFileName),
                    SourceName: storageFolder.Name,
                    SizeBytes: file.SizeBytes,
                    IsAlreadyTransferred: false
                ))
                .ToList()
        );

        try
        {
            foreach (var file in archiveFilesWithSize)
            {
                var fileName = Path.GetFileName(file.ArchiveFile.FullFileName);
                var targetFilePath = Path.Join(targetFolderPath, fileName);

                await fileSystemService.CopyFileAsync(
                    sourceFilePath: file.ArchiveFile.FullFileName,
                    destinationFilePath: targetFilePath,
                    progress: new TransferProgressReporter(
                        tracker: progressTracker,
                        identifier: transferIdentifier,
                        fileId: file.ArchiveFile.Id,
                        fileName: fileName,
                        sourceName: storageFolder.Name
                    ),
                    cancellationToken: cancellationToken
                );

                copiedFilePaths.Add(targetFilePath);

                var copiedSizeBytes = fileSystemService.GetFileSizeBytes(targetFilePath);

                if (copiedSizeBytes != file.SizeBytes)
                {
                    throw new IOException(
                        $"Copied file {targetFilePath} has {copiedSizeBytes} bytes instead of {file.SizeBytes} bytes"
                    );
                }
            }
        }
        finally
        {
            progressTracker.StopTracking(transferIdentifier);
        }
    }

    private void DeleteCopiedFiles(
        Archive archive,
        List<string> copiedFilePaths,
        string? targetFolderPath
    )
    {
        try
        {
            foreach (var copiedFilePath in copiedFilePaths)
            {
                fileSystemService.DeleteFileIfExists(copiedFilePath);
            }

            if (targetFolderPath is not null)
            {
                fileSystemService.DeleteDirectoryIfEmpty(targetFolderPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Could not delete the files copied to {TargetFolderPath} while moving archive {ArchiveId}",
                targetFolderPath,
                archive.Id
            );
        }
    }

    private async Task PointArchiveToStorageFolderAndDeleteLocalFilesAsync(
        Archive archive,
        ArchiveCopiedIntoStorageFolder copiedArchive,
        CancellationToken cancellationToken
    )
    {
        if (await ArchiveChangedDuringCopyAsync(archive, copiedArchive, cancellationToken))
        {
            DeleteCopiedFiles(
                archive: archive,
                copiedFilePaths: copiedArchive.CopiedFilePaths,
                targetFolderPath: copiedArchive.TargetFolderPath
            );

            return;
        }

        var localArchiveFolderPath = archive.ArchiveFolderPath;
        var localFilePaths = archive.ArchiveFiles.Select(file => file.FullFileName).ToList();

        foreach (var archiveFile in archive.ArchiveFiles)
        {
            archiveFile.FullFileName = Path.Join(
                copiedArchive.TargetFolderPath,
                Path.GetFileName(archiveFile.FullFileName)
            );
            archiveFile.Md5HashInStorageFolder = archiveFile.Md5Hash;
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

    private sealed record ArchiveFileWithSize(ArchiveFile ArchiveFile, long SizeBytes);

    private sealed record ArchiveCopiedIntoStorageFolder(
        ArchiveStorageFolder StorageFolder,
        string TargetFolderPath,
        IReadOnlyList<ArchiveFileWithSize> ArchiveFilesWithSize,
        List<string> CopiedFilePaths
    );

    private sealed record ArchiveIdsWithUnresolvedNotification(
        HashSet<int> NoStorageFolderAvailable,
        HashSet<int> MoveFailed
    );
}
