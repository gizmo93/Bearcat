namespace Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.ReadModels;

public sealed record SkippedArchiveConfigTemplateReadModel(
    int ArchiveConfigTemplateId,
    int ReleaseTemplateId,
    string ReleaseTemplateName,
    string ArchiveConfigTemplateName,
    string HosterName,
    int MaxArchiveFileSize
);
