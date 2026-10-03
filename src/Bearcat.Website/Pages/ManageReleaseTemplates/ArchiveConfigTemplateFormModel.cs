namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public class ArchiveConfigTemplateFormModel
{
    public string Name { get; set; } = string.Empty;

    public string? ArchiverName { get; set; }

    public string ArchiveFilesBasePath { get; set; } = string.Empty;

    public string? ArchivePassword { get; set; }

    public int ArchiveFileSizeMb { get; set; }

    public bool UseReleaseNameAsArchiveName { get; set; }

    public bool PackReleaseFolderAsRootFolder { get; set; } = true;

    public bool CreateNonceFile { get; set; } = true;

    public bool IsEdit { get; set; }

    public IEnumerable<int>? AdditionalArchiveContentIds { get; set; }

    public IReadOnlyList<int> GetAdditionalArchiveContentIds()
    {
        return AdditionalArchiveContentIds?.ToList() ?? [];
    }
}
