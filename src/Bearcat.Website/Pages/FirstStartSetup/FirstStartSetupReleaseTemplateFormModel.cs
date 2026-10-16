namespace Bearcat.Website.Pages.FirstStartSetup;

public class FirstStartSetupReleaseTemplateFormModel
{
    public int? ExistingReleaseTemplateId { get; set; }

    public int? ReleaseGroupId { get; set; }

    public string ReleaseTemplateName { get; set; } = string.Empty;

    public string? ArchiverName { get; set; }

    public int ArchiveFileSizeMb { get; set; }

    public string? ArchivePassword { get; set; }

    public string ArchiveOutputFolderPath { get; set; } = string.Empty;
}
