using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageUploadConfigs;

public class UploadConfigFormModel
{
    public string? Name { get; set; }

    public UploadSource UploadSource { get; set; } = UploadSource.ArchiveFiles;

    public UploadTarget? UploadTarget { get; set; }

    public int? ArchiveConfigId { get; set; }

    public bool PremiumOnlyDownload { get; set; }
}
