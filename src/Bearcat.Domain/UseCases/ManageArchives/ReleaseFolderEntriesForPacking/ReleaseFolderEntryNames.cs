using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;

public static class ReleaseFolderEntryNames
{
    public static readonly StringComparer EntryNameComparer = StringComparer.OrdinalIgnoreCase;

    public static string GetFullSourcePath(string sourcePath)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourcePath));
    }

    public static string GetEntryName(AdditionalArchiveContent content)
    {
        return content.Type switch
        {
            AdditionalArchiveContentType.Path => Path.GetFileName(
                GetFullSourcePath(content.SourcePath!)
            ),
            AdditionalArchiveContentType.TextFile => content.FileName!,
            _ => throw new ArgumentOutOfRangeException(
                nameof(content),
                $"Unknown additional archive content type, {content.Type}"
            ),
        };
    }

    public static List<DuplicatedReleaseFolderEntryName<TEntry>> FindDuplicatedEntryNames<TEntry>(
        IReadOnlyList<TEntry> entries,
        Func<TEntry, string> getEntryName
    )
    {
        return entries
            .GroupBy(getEntryName, EntryNameComparer)
            .Where(group => group.Count() > 1)
            .Select(group => new DuplicatedReleaseFolderEntryName<TEntry>(
                group.Key,
                group.ToList()
            ))
            .ToList();
    }
}
