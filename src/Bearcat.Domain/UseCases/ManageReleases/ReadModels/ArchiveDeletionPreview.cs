namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ArchiveDeletionPreview(
    bool CanDelete,
    IReadOnlyList<string> DeletableArchiveFolderPaths,
    IReadOnlyList<string> MirrorHosterNames
);
