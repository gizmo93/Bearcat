namespace Bearcat.Domain.UseCases.ManageArchives.ArchiveFolderRelocations;

public record ArchiveFolderRelocationPreviewItem(
    int ArchiveId,
    string ReleaseName,
    string ArchiveConfigName,
    string CurrentArchiveFolderPath,
    string? FoundArchiveFolderPath,
    ArchiveFolderNotRelocatableReason? NotRelocatableReason
)
{
    public bool IsRelocatable => NotRelocatableReason is null;
}
