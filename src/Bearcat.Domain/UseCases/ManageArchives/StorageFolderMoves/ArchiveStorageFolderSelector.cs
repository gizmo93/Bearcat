using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageArchives.StorageFolderMoves;

public static class ArchiveStorageFolderSelector
{
    public const long BytesPerGigabyte = 1024L * 1024 * 1024;

    public static ArchiveStorageFolder? SelectStorageFolder(
        IReadOnlyList<ArchiveStorageFolderWithAvailableFreeSpace> storageFolders,
        long archiveSizeBytes
    )
    {
        return storageFolders
            .Where(folder =>
                folder.StorageFolder.IsActive
                && folder.AvailableFreeSpaceBytes is { } availableFreeSpaceBytes
                && availableFreeSpaceBytes
                    >= archiveSizeBytes + folder.StorageFolder.MinimumFreeSpaceGb * BytesPerGigabyte
            )
            .OrderBy(folder => folder.StorageFolder.Priority)
            .ThenByDescending(folder => folder.AvailableFreeSpaceBytes)
            .ThenBy(folder => folder.StorageFolder.Id)
            .Select(folder => folder.StorageFolder)
            .FirstOrDefault();
    }
}
