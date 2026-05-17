namespace Bearcat.Domain.UseCases.ManageReleaseGroups.Dto;

public record ReleaseGroupDto(
    int ReleaseGroupId,
    string Name,
    bool EnableAutomaticReuploads,
    int NumberOfHoursUntilReupload,
    int AssignedReleaseCount
);
