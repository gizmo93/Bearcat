using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.ManageArchives.StorageFolderLocalWorkingCopies;

public class ArchiveLocalWorkingCopyCleanupService(
    IArchiveCleanupRepository repository,
    IFileSystemService fileSystemService,
    ILogger<ArchiveLocalWorkingCopyCleanupService> logger
)
{
    public async Task DeleteLocalWorkingCopiesAsync(CancellationToken cancellationToken)
    {
        var archives =
            await repository.GetArchivesWithLocalWorkingCopyFilesWithoutWaitingPendingOrUploadingUploadAsync(
                cancellationToken
            );

        foreach (var archive in archives)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var localWorkingCopyFiles = ArchiveLocalWorkingCopyFiles.GetLocalWorkingCopyFiles(
                archive
            );

            await DeleteLocalWorkingCopyAsync(archive, localWorkingCopyFiles, cancellationToken);
        }
    }

    private async Task DeleteLocalWorkingCopyAsync(
        Archive archive,
        IReadOnlyList<ArchiveFile> localWorkingCopyFiles,
        CancellationToken cancellationToken
    )
    {
        var localFilePaths = localWorkingCopyFiles
            .Select(archiveFile => archiveFile.FullFileName)
            .ToList();

        foreach (var archiveFile in localWorkingCopyFiles)
        {
            archiveFile.Md5Hash = archiveFile.Md5HashInStorageFolder;
            archiveFile.FullFileName = Path.Join(
                archive.ArchiveFolderPath,
                Path.GetFileName(archiveFile.FullFileName)
            );
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Pointed {FileCount} files of archive {ArchiveId} back to its storage folder at {ArchiveFolderPath}",
            localWorkingCopyFiles.Count,
            archive.Id,
            archive.ArchiveFolderPath
        );

        try
        {
            foreach (var localFilePath in localFilePaths)
            {
                fileSystemService.DeleteFileIfExists(localFilePath);
            }

            foreach (
                var localFolderPath in localFilePaths
                    .Select(localFilePath => Path.GetDirectoryName(localFilePath)!)
                    .Distinct()
            )
            {
                fileSystemService.DeleteDirectoryIfEmpty(localFolderPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Pointed archive {ArchiveId} back to its storage folder but could not delete the local working copy files {FilePaths}",
                archive.Id,
                string.Join(", ", localFilePaths)
            );
        }
    }
}
