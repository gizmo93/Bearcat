using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ArchiveFileCopies;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.StorageFolderLocalWorkingCopies;

public class ArchiveLocalWorkingCopyService(
    IArchiveCleanupRepository repository,
    IFileSystemService fileSystemService,
    INotificationService notificationService,
    ArchiveFileCopyService archiveFileCopyService,
    ITransferCancellationRegistry cancellationRegistry,
    ILogger<ArchiveLocalWorkingCopyService> logger
)
{
    public static bool StorageFolderRequiresLocalWorkingCopyBeforeReupload(Archive archive)
    {
        return archive.ArchiveStorageFolder is { UseLocalWorkingCopyForReuploads: true };
    }

    public static List<ArchiveFile> GetArchiveFilesToCopyIntoLocalWorkingCopy(
        Archive archive,
        IReadOnlyList<ArchiveFile> archiveFilesToUpload
    )
    {
        if (!StorageFolderRequiresLocalWorkingCopyBeforeReupload(archive))
        {
            return [];
        }

        return archiveFilesToUpload
            .Where(archiveFile =>
                !ArchiveLocalWorkingCopyFiles.IsLocalWorkingCopyFile(archive, archiveFile)
            )
            .OrderBy(archiveFile => archiveFile.Id)
            .ToList();
    }

    public static bool ArchiveFileStaysUnchangedInStorageFolder(
        Archive archive,
        ArchiveFile archiveFile,
        IReadOnlyList<ArchiveFile> archiveFilesToUpload
    )
    {
        return StorageFolderRequiresLocalWorkingCopyBeforeReupload(archive)
            && ArchiveLocalWorkingCopyFiles.IsFileInStorageFolder(archive, archiveFile)
            && !archiveFilesToUpload.Contains(archiveFile);
    }

    public async Task CopyArchiveFilesIntoLocalWorkingCopyAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> archiveFilesToCopy,
        CancellationToken cancellationToken
    )
    {
        var storageFolder = archive.ArchiveStorageFolder!;

        var transferIdentifier = new TransferIdentifier(
            TransferType.ArchiveCopyIntoLocalWorkingCopy,
            archive.Id
        );

        using var userCancellableTransfer = cancellationRegistry.RegisterUserCancellableTransfer(
            transferIdentifier,
            cancellationToken
        );

        var copiedFiles = await CopyArchiveFilesAsync(
            archive: archive,
            archiveFilesToCopy: archiveFilesToCopy,
            storageFolder: storageFolder,
            transferIdentifier: transferIdentifier,
            userCancellableTransfer: userCancellableTransfer,
            cancellationToken: cancellationToken
        );

        if (copiedFiles is null)
        {
            return;
        }

        foreach (var copiedFile in copiedFiles)
        {
            copiedFile.ArchiveFile.FullFileName = copiedFile.CopiedFilePath;
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Copied {FileCount} files of archive {ArchiveId} from storage folder {StorageFolderName} into a local working copy",
            copiedFiles.Count,
            archive.Id,
            storageFolder.Name
        );
    }

    private async Task<IReadOnlyList<CopiedArchiveFile>?> CopyArchiveFilesAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> archiveFilesToCopy,
        ArchiveStorageFolder storageFolder,
        TransferIdentifier transferIdentifier,
        UserCancellableTransfer userCancellableTransfer,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var archiveFilesBasePath = archive.ArchiveConfig.ArchiveFilesBasePath;

            if (!fileSystemService.DirectoryExists(archiveFilesBasePath))
            {
                throw new DirectoryNotFoundException(
                    $"Archive files base path {archiveFilesBasePath} does not exist and Bearcat does not create it"
                );
            }

            var archiveFilesWithSize = archiveFileCopyService.GetArchiveFilesWithSize(
                archiveFilesToCopy
            );

            var workingCopyFolderPath = GetWorkingCopyFolderPath(archiveFilesBasePath, archive);

            return await archiveFileCopyService.CopyArchiveFilesAndVerifySizesAsync(
                archiveId: archive.Id,
                archiveFilesWithSize: archiveFilesWithSize,
                targetFolderPath: workingCopyFolderPath,
                sourceName: storageFolder.Name,
                transferIdentifier: transferIdentifier,
                cancellationToken: userCancellableTransfer.CancellationToken
            );
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException && userCancellableTransfer.WasCanceledByUser)
            {
                logger.LogInformation(
                    "Canceled copying archive {ArchiveId} from storage folder {StorageFolderName} into a local working copy on user request, the upload reads the archive files from {ArchiveFolderPath}",
                    archive.Id,
                    storageFolder.Name,
                    archive.ArchiveFolderPath
                );

                return null;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            await ReportCopyFailedAsync(
                archive: archive,
                storageFolder: storageFolder,
                exception: ex,
                cancellationToken: cancellationToken
            );

            return null;
        }
    }

    private string GetWorkingCopyFolderPath(string archiveFilesBasePath, Archive archive)
    {
        var existingWorkingCopyFile = archive.ArchiveFiles.FirstOrDefault(archiveFile =>
            ArchiveLocalWorkingCopyFiles.IsLocalWorkingCopyFile(archive, archiveFile)
        );

        if (existingWorkingCopyFile is not null)
        {
            return Path.GetDirectoryName(existingWorkingCopyFile.FullFileName)!;
        }

        var archiveFolderName = Path.GetFileName(
            Path.TrimEndingDirectorySeparator(archive.ArchiveFolderPath)
        );

        var workingCopyFolderPath = Path.Join(archiveFilesBasePath, archiveFolderName);

        if (!fileSystemService.DirectoryHasEntries(workingCopyFolderPath))
        {
            return workingCopyFolderPath;
        }

        return Path.Join(archiveFilesBasePath, $"{archiveFolderName}.{archive.Id}");
    }

    private async Task ReportCopyFailedAsync(
        Archive archive,
        ArchiveStorageFolder storageFolder,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        logger.LogWarning(
            exception,
            "Could not copy archive {ArchiveId} from storage folder {StorageFolderName} into a local working copy, the upload reads the archive files from {ArchiveFolderPath}",
            archive.Id,
            storageFolder.Name,
            archive.ArchiveFolderPath
        );

        var archiveIdsWithUnresolvedNotification =
            await repository.GetArchiveIdsWithUnresolvedNotificationAsync(
                NotificationKind.ArchiveCopyIntoLocalWorkingCopyFailed,
                cancellationToken
            );

        if (archiveIdsWithUnresolvedNotification.Contains(archive.Id))
        {
            return;
        }

        notificationService.Create(
            kind: NotificationKind.ArchiveCopyIntoLocalWorkingCopyFailed,
            message: $"Archive {archive.Id} of release '{archive.ArchiveConfig.Release.Name}' could not be copied from storage folder '{storageFolder.Name}' into a local working copy: {exception.Message.TrimEnd('.')}. The upload reads the archive files from {archive.ArchiveFolderPath}.",
            entity: archive,
            selector: n => n.Archive
        );

        await repository.SaveChangesAsync(cancellationToken);
    }
}
