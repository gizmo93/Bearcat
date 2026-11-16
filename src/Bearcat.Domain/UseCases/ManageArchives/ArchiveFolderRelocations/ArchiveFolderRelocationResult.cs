namespace Bearcat.Domain.UseCases.ManageArchives.ArchiveFolderRelocations;

public record ArchiveFolderRelocationResult(
    int RelocatedArchiveCount,
    IReadOnlyList<ArchiveFolderRelocationPreviewItem> NotRelocatedArchives
);
