namespace Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.ReadModels;

public sealed record ArchiveConfigTemplateUploadHosterRow(
    int ArchiveConfigTemplateId,
    int ReleaseTemplateId,
    string ReleaseTemplateName,
    string ArchiveConfigTemplateName,
    string HosterClassName
);
