namespace Bearcat.Domain.UseCases.ManageArchives.Reuploads;

public enum ExistingArchiveAssignmentResult
{
    Assigned,
    NewArchiveRequired,
    PostponedBecauseArchiveHasActiveUpload,
    ArchiveFilesMissing,
    HashChangeCanceledByUser,
}
