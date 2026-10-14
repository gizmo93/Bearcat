using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;

namespace Bearcat.Website.Pages.ManageUploadConfigs;

public class UploadConfigFormModel
{
    public string? Name { get; set; }

    public UploadTarget? UploadTarget { get; set; }

    public int? ArchiveConfigId { get; set; }

    public bool PremiumOnlyDownload { get; set; }
}
