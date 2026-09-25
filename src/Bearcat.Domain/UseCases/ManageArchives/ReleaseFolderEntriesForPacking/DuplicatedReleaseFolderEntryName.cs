namespace Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;

public record DuplicatedReleaseFolderEntryName<TEntry>(
    string EntryName,
    IReadOnlyList<TEntry> Entries
);
