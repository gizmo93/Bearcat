namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public class CreateReleaseFromTemplateFormModel
{
    public int? ReleaseTemplateId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string FolderPath { get; set; } = string.Empty;
}
