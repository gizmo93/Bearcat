using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.ArchiveRetention;

public class LocalArchiveDeleter(IFileSystemService fileSystemService)
{
    public void DeleteLocalArchive(Archive archive)
    {
        foreach (var archiveFile in archive.ArchiveFiles)
        {
            fileSystemService.DeleteFileIfExists(archiveFile.FullFileName);
        }

        fileSystemService.DeleteDirectoryIfEmpty(archive.ArchiveFolderPath);
        archive.ArchiveState = ArchiveState.Deleted;
    }
}
