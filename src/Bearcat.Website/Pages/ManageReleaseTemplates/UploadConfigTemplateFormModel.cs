namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public class UploadConfigTemplateFormModel
{
    public string? Name { get; set; }

    public int? HosterRegistrationId { get; set; }

    public int? ArchiveConfigTemplateId { get; set; }

    public bool PremiumOnlyDownload { get; set; }

    public List<string> LinksDistributedTo { get; set; } = [];

    public bool IsEdit { get; set; }
}
