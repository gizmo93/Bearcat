namespace Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;

public record ReleaseFolderEntryForPacking(
    string Name,
    string SourceDescription,
    ReleaseFolderEntryForPackingType Type,
    string? SourcePath,
    string? TextContent
);
