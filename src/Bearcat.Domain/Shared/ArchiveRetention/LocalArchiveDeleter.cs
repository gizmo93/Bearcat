using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.FolderConfirmation;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.ArchiveRetention;

public class LocalArchiveDeleter(
    IFileSystemService fileSystemService,
    FolderConfirmationCheck folderConfirmationCheck
)
{
    public async Task<FolderConfirmationResult> DeleteLocalArchiveAsync(
        Archive archive,
        CancellationToken cancellationToken
    )
    {
        var folderConfirmation = await folderConfirmationCheck.GetFolderConfirmationAsync(
            archive.ArchiveFolderPath,
            cancellationToken
        );

        if (!folderConfirmation.IsWriteAllowed)
        {
            return folderConfirmation;
        }

        foreach (var archiveFile in archive.ArchiveFiles)
        {
            fileSystemService.DeleteFileIfExists(archiveFile.FullFileName);
        }

        fileSystemService.DeleteDirectoryIfEmpty(archive.ArchiveFolderPath);
        archive.ArchiveState = ArchiveState.Deleted;

        return folderConfirmation;
    }
}
