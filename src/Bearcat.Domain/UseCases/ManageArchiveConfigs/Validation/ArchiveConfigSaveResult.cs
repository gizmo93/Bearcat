using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Assignment;

namespace Bearcat.Domain.UseCases.ManageArchiveConfigs.Validation;

public record ArchiveConfigSaveResult(
    int? ArchiveConfigId,
    IReadOnlyList<DuplicatedEntryName> AdditionalArchiveContentEntryNameCollisions
)
{
    public bool IsSuccess => AdditionalArchiveContentEntryNameCollisions.Count == 0;

    public static ArchiveConfigSaveResult Saved(int archiveConfigId)
    {
        return new ArchiveConfigSaveResult(archiveConfigId, []);
    }

    public static ArchiveConfigSaveResult Invalid(
        IReadOnlyList<DuplicatedEntryName> additionalArchiveContentEntryNameCollisions
    )
    {
        return new ArchiveConfigSaveResult(null, additionalArchiveContentEntryNameCollisions);
    }
}
