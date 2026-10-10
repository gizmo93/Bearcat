namespace Bearcat.Domain.UseCases.ManageArchiveStorageFolders.ReadModels;

public record ArchiveStorageFolderReadModel(
    int Id,
    string Name,
    string Path,
    bool IsActive,
    int MinimumFreeSpaceGb,
    int Priority,
    bool RetrieveArchivesBeforeReupload,
    int StoredArchiveCount
);
