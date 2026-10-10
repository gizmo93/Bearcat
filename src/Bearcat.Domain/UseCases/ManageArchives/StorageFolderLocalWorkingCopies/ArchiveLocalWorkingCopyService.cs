using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.StorageFolderLocalWorkingCopies;

public class ArchiveLocalWorkingCopyService(
    IArchiveCleanupRepository repository,
    IFileSystemService fileSystemService,
    INotificationService notificationService,
    ITransferProgressTracker progressTracker,
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

        var userCancellationToken = cancellationRegistry.Register(transferIdentifier);

        try
        {
            var copiedFilePathPerArchiveFile = await CopyArchiveFilesAsync(
                archive: archive,
                archiveFilesToCopy: archiveFilesToCopy,
                storageFolder: storageFolder,
                transferIdentifier: transferIdentifier,
                userCancellationToken: userCancellationToken,
                cancellationToken: cancellationToken
            );

            if (copiedFilePathPerArchiveFile is null)
            {
                return;
            }

            foreach (var (archiveFile, copiedFilePath) in copiedFilePathPerArchiveFile)
            {
                archiveFile.FullFileName = copiedFilePath;
            }

            await repository.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Copied {FileCount} files of archive {ArchiveId} from storage folder {StorageFolderName} into a local working copy",
                copiedFilePathPerArchiveFile.Count,
                archive.Id,
                storageFolder.Name
            );
        }
        finally
        {
            cancellationRegistry.Unregister(transferIdentifier);
        }
    }

    private async Task<Dictionary<ArchiveFile, string>?> CopyArchiveFilesAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> archiveFilesToCopy,
        ArchiveStorageFolder storageFolder,
        TransferIdentifier transferIdentifier,
        CancellationToken userCancellationToken,
        CancellationToken cancellationToken
    )
    {
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            userCancellationToken
        );

        var copiedFilePathPerArchiveFile = new Dictionary<ArchiveFile, string>();
        string? workingCopyFolderPath = null;

        try
        {
            var archiveFilesBasePath = archive.ArchiveConfig.ArchiveFilesBasePath;

            if (!fileSystemService.DirectoryExists(archiveFilesBasePath))
            {
                throw new DirectoryNotFoundException(
                    $"Archive files base path {archiveFilesBasePath} does not exist and Bearcat does not create it"
                );
            }

            var archiveFilesWithSize = archiveFilesToCopy
                .Select(file => new ArchiveFileWithSize(
                    file,
                    fileSystemService.GetFileSizeBytes(file.FullFileName)
                ))
                .ToList();

            workingCopyFolderPath = GetWorkingCopyFolderPath(archiveFilesBasePath, archive);

            await CopyAndVerifyArchiveFilesAsync(
                archiveFilesWithSize: archiveFilesWithSize,
                storageFolder: storageFolder,
                workingCopyFolderPath: workingCopyFolderPath,
                copiedFilePathPerArchiveFile: copiedFilePathPerArchiveFile,
                transferIdentifier: transferIdentifier,
                cancellationToken: linkedTokenSource.Token
            );

            return copiedFilePathPerArchiveFile;
        }
        catch (Exception ex)
        {
            DeleteCopiedFiles(
                archive,
                copiedFilePathPerArchiveFile.Values.ToList(),
                workingCopyFolderPath
            );

            if (ex is OperationCanceledException && userCancellationToken.IsCancellationRequested)
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

    private async Task CopyAndVerifyArchiveFilesAsync(
        IReadOnlyList<ArchiveFileWithSize> archiveFilesWithSize,
        ArchiveStorageFolder storageFolder,
        string workingCopyFolderPath,
        Dictionary<ArchiveFile, string> copiedFilePathPerArchiveFile,
        TransferIdentifier transferIdentifier,
        CancellationToken cancellationToken
    )
    {
        fileSystemService.CreateDirectory(workingCopyFolderPath);

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
                var workingCopyFilePath = Path.Join(workingCopyFolderPath, fileName);

                await fileSystemService.CopyFileAsync(
                    sourceFilePath: file.ArchiveFile.FullFileName,
                    destinationFilePath: workingCopyFilePath,
                    progress: new TransferProgressReporter(
                        tracker: progressTracker,
                        identifier: transferIdentifier,
                        fileId: file.ArchiveFile.Id,
                        fileName: fileName,
                        sourceName: storageFolder.Name
                    ),
                    cancellationToken: cancellationToken
                );

                copiedFilePathPerArchiveFile[file.ArchiveFile] = workingCopyFilePath;

                var copiedSizeBytes = fileSystemService.GetFileSizeBytes(workingCopyFilePath);

                if (copiedSizeBytes != file.SizeBytes)
                {
                    throw new IOException(
                        $"Copied file {workingCopyFilePath} has {copiedSizeBytes} bytes instead of {file.SizeBytes} bytes"
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
        IReadOnlyList<string> copiedFilePaths,
        string? workingCopyFolderPath
    )
    {
        try
        {
            foreach (var copiedFilePath in copiedFilePaths)
            {
                fileSystemService.DeleteFileIfExists(copiedFilePath);
            }

            if (workingCopyFolderPath is not null)
            {
                fileSystemService.DeleteDirectoryIfEmpty(workingCopyFolderPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Could not delete the files copied to {WorkingCopyFolderPath} while copying archive {ArchiveId} from its storage folder",
                workingCopyFolderPath,
                archive.Id
            );
        }
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

    private sealed record ArchiveFileWithSize(ArchiveFile ArchiveFile, long SizeBytes);
}
