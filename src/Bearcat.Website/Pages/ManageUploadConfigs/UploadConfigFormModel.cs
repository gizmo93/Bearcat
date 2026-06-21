namespace Bearcat.Website.Pages.ManageUploadConfigs;

public class UploadConfigFormModel
{
    public string? Name { get; set; }

    public int? HosterRegistrationId { get; set; }

    public int? ArchiveConfigId { get; set; }

    public bool PremiumOnlyDownload { get; set; }
}
