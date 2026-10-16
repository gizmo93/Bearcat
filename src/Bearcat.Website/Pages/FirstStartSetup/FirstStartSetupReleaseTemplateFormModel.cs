using Bearcat.Domain.UseCases.FirstStartSetup.ReleaseTemplates;

namespace Bearcat.Website.Pages.FirstStartSetup;

public class FirstStartSetupReleaseTemplateFormModel
{
    public string ReleaseGroupName { get; set; } = string.Empty;

    public bool EnableAutomaticReuploads { get; set; }

    public int NumberOfHoursUntilReupload { get; set; }

    public string ReleaseTemplateName { get; set; } = string.Empty;

    public string? ArchiverName { get; set; }

    public int ArchiveFileSizeMb { get; set; }

    public string? ArchivePassword { get; set; }

    public string ArchiveOutputFolderPath { get; set; } = string.Empty;

    public int? ExistingReleaseTemplateId { get; set; }

    public FirstStartReleaseTemplateInput ToInput(List<int> hosterRegistrationIds)
    {
        return new FirstStartReleaseTemplateInput(
            ReleaseGroupName: ReleaseGroupName,
            EnableAutomaticReuploads: EnableAutomaticReuploads,
            NumberOfHoursUntilReupload: NumberOfHoursUntilReupload,
            ReleaseTemplateName: ReleaseTemplateName,
            ArchiverName: ArchiverName!,
            ArchiveFileSizeMb: ArchiveFileSizeMb,
            ArchivePassword: string.IsNullOrWhiteSpace(ArchivePassword) ? null : ArchivePassword,
            ArchiveOutputFolderPath: ArchiveOutputFolderPath,
            HosterRegistrationIds: hosterRegistrationIds
        );
    }
}
