using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Assignment;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.Validation;

public record ArchiveConfigTemplateSaveResult(
    int? ArchiveConfigTemplateId,
    IReadOnlyList<DuplicatedEntryName> AdditionalArchiveContentEntryNameCollisions
)
{
    public bool IsSuccess => AdditionalArchiveContentEntryNameCollisions.Count == 0;

    public static ArchiveConfigTemplateSaveResult Saved(int archiveConfigTemplateId)
    {
        return new ArchiveConfigTemplateSaveResult(archiveConfigTemplateId, []);
    }

    public static ArchiveConfigTemplateSaveResult Invalid(
        IReadOnlyList<DuplicatedEntryName> additionalArchiveContentEntryNameCollisions
    )
    {
        return new ArchiveConfigTemplateSaveResult(
            null,
            additionalArchiveContentEntryNameCollisions
        );
    }
}
