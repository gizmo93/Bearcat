namespace Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.ReadModels;

public sealed record SkippedArchiveConfigReadModel(
    int ArchiveConfigId,
    int ReleaseId,
    string ReleaseName,
    string ArchiveConfigName,
    string HosterName,
    int MaxArchiveFileSize
);
