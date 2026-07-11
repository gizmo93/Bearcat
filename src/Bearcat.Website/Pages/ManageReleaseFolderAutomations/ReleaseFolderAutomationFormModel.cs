namespace Bearcat.Website.Pages.ManageReleaseFolderAutomations;

public class ReleaseFolderAutomationFormModel
{
    public int? ReleaseFolderAutomationId { get; set; }

    public string BasePath { get; set; } = string.Empty;

    public string? FolderNamePattern { get; set; }

    public int? ReleaseTemplateId { get; set; }

    public string PrimaryLanguageCode { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public bool IsEdit { get; set; }
}
