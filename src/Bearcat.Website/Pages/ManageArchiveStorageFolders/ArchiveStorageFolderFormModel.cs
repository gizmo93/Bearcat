using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Dto;

namespace Bearcat.Website.Pages.ManageArchiveStorageFolders;

public class ArchiveStorageFolderFormModel
{
    public int? ArchiveStorageFolderId { get; set; }

    public string? Name { get; set; }

    public string? Path { get; set; }

    public int MinimumFreeSpaceGb { get; set; }

    public int Priority { get; set; } = 1;

    public bool RetrieveArchivesBeforeReupload { get; set; }

    public ArchiveStorageFolderInput ToInput()
    {
        return new ArchiveStorageFolderInput(
            Name ?? string.Empty,
            Path ?? string.Empty,
            MinimumFreeSpaceGb,
            Priority,
            RetrieveArchivesBeforeReupload
        );
    }
}
