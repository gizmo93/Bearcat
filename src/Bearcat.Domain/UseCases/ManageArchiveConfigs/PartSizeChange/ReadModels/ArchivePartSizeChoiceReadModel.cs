namespace Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.ReadModels;

public sealed record ArchivePartSizeChoiceReadModel(
    int ArchiveFileSizeMb,
    int ArchiveConfigCount,
    int ArchiveConfigTemplateCount
);
