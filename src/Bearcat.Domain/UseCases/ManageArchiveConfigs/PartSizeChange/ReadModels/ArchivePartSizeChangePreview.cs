namespace Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.ReadModels;

public sealed record ArchivePartSizeChangePreview(
    int ArchiveConfigCountToChange,
    int ArchiveConfigTemplateCountToChange,
    IReadOnlyList<SkippedArchiveConfigReadModel> SkippedArchiveConfigs,
    IReadOnlyList<SkippedArchiveConfigTemplateReadModel> SkippedArchiveConfigTemplates
);
