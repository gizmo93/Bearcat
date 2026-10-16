namespace Bearcat.Domain.UseCases.FirstStartSetup.ReleaseTemplates;

public sealed record FirstStartReleaseTemplateInput(
    string ReleaseGroupName,
    bool EnableAutomaticReuploads,
    int NumberOfHoursUntilReupload,
    string ReleaseTemplateName,
    string ArchiverName,
    int ArchiveFileSizeMb,
    string? ArchivePassword,
    string ArchiveOutputFolderPath,
    List<int> HosterRegistrationIds
);
