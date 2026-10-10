namespace Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Validation;

public record ArchiveStorageFolderSaveResult(
    int? ArchiveStorageFolderId,
    IReadOnlyList<ArchiveStorageFolderValidationError> ValidationErrors
)
{
    public bool IsSuccess => ValidationErrors.Count == 0;

    public static ArchiveStorageFolderSaveResult Saved(int archiveStorageFolderId)
    {
        return new ArchiveStorageFolderSaveResult(archiveStorageFolderId, []);
    }

    public static ArchiveStorageFolderSaveResult Invalid(
        IReadOnlyList<ArchiveStorageFolderValidationError> validationErrors
    )
    {
        return new ArchiveStorageFolderSaveResult(null, validationErrors);
    }
}
