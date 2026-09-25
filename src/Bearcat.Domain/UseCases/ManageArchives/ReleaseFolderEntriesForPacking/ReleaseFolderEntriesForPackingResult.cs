namespace Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;

public record ReleaseFolderEntriesForPackingResult(
    IReadOnlyList<ReleaseFolderEntryForPacking> Entries,
    IReadOnlyList<string> ErrorMessages
);
