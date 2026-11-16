namespace Bearcat.Domain.UseCases.ManageArchiveConfigs.PartSizeChange.ReadModels;

public sealed record ArchiveConfigUploadHosterRow(
    int ArchiveConfigId,
    int ReleaseId,
    string ReleaseName,
    string ArchiveConfigName,
    string HosterClassName
);
