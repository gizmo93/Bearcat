namespace Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.Dto;

public sealed record ArchivePartSizeChangeRequest(
    int CurrentArchiveFileSizeMb,
    int NewArchiveFileSizeMb,
    bool IncludeArchiveConfigTemplates
);
