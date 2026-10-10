namespace Bearcat.Domain.UseCases.ManageArchiveStorageFolders.ReadModels;

public record ArchiveStorageFolderWithAvailableFreeSpaceReadModel(
    ArchiveStorageFolderReadModel StorageFolder,
    long? AvailableFreeSpaceBytes
);
