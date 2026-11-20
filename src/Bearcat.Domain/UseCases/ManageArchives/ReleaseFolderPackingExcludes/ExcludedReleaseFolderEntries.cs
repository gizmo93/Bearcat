namespace Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderPackingExcludes;

public record ExcludedReleaseFolderEntries(
    IReadOnlyList<string> RelativePaths,
    int FileCount,
    long TotalBytes
);
