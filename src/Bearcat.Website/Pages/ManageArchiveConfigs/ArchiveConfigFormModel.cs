namespace Bearcat.Website.Pages.ManageArchiveConfigs;

public class ArchiveConfigFormModel
{
    public string? ArchiveFilesBasePath { get; set; }

    public string? ArchiverName { get; set; }

    public string? ArchiveNamePrefix { get; set; }

    public string? ArchivePassword { get; set; }

    public int ArchiveFileSizeMb { get; set; }

    public bool PackReleaseFolderAsRootFolder { get; set; } = true;

    public bool CreateNonceFile { get; set; } = true;

    public string? Name { get; set; }

    public IEnumerable<int>? AdditionalArchiveContentIds { get; set; }

    public IReadOnlyList<int> GetAdditionalArchiveContentIds()
    {
        return AdditionalArchiveContentIds?.ToList() ?? [];
    }
}
