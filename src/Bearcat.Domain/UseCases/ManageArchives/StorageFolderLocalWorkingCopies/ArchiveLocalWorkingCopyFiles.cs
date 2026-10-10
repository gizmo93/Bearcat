using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases;

namespace Bearcat.Domain.UseCases.ManageArchives.StorageFolderLocalWorkingCopies;

public static class ArchiveLocalWorkingCopyFiles
{
    public static bool IsLocalWorkingCopyFile(Archive archive, ArchiveFile archiveFile)
    {
        return archive.ArchiveStorageFolderId is not null
            && !FolderPathHelper.IsSameOrSubPath(
                archiveFile.FullFileName,
                archive.ArchiveFolderPath
            );
    }

    public static bool IsFileInStorageFolder(Archive archive, ArchiveFile archiveFile)
    {
        return archive.ArchiveStorageFolderId is not null
            && FolderPathHelper.IsSameOrSubPath(
                archiveFile.FullFileName,
                archive.ArchiveFolderPath
            );
    }

    public static List<ArchiveFile> GetLocalWorkingCopyFiles(Archive archive)
    {
        return archive
            .ArchiveFiles.Where(archiveFile => IsLocalWorkingCopyFile(archive, archiveFile))
            .OrderBy(archiveFile => archiveFile.Id)
            .ToList();
    }
}
