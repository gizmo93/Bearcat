using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageArchives.StorageFolderMoves;

public sealed record ArchiveStorageFolderWithAvailableFreeSpace(
    ArchiveStorageFolder StorageFolder,
    long? AvailableFreeSpaceBytes
);
