namespace Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Dto;

public record ArchiveStorageFolderInput(
    string Name,
    string Path,
    int MinimumFreeSpaceGb,
    int Priority,
    bool RetrieveArchivesBeforeReupload
);
