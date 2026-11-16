namespace Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.Dto;

public sealed record ArchivePartSizeChangeResult(
    int ChangedArchiveConfigCount,
    int ChangedArchiveConfigTemplateCount,
    int SkippedArchiveConfigCount,
    int SkippedArchiveConfigTemplateCount
);
