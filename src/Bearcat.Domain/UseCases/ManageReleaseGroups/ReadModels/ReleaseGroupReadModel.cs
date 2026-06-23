namespace Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;

public record ReleaseGroupReadModel(
    int ReleaseGroupId,
    string Name,
    bool EnableAutomaticReuploads,
    int NumberOfHoursUntilReupload,
    int AssignedReleaseCount,
    int? QualityProfileId,
    string? QualityProfileName
);
