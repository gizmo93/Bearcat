using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;

namespace Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Assignment;

public static class AdditionalArchiveContentAssignmentValidation
{
    public static List<DuplicatedEntryName> FindDuplicatedEntryNames(
        IReadOnlyList<AdditionalArchiveContent> additionalArchiveContents
    )
    {
        return ReleaseFolderEntryNames
            .FindDuplicatedEntryNames(
                additionalArchiveContents,
                ReleaseFolderEntryNames.GetEntryName
            )
            .Select(duplicatedEntryName => new DuplicatedEntryName(
                duplicatedEntryName.EntryName,
                duplicatedEntryName
                    .Entries.Select(content => content.Name)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            ))
            .ToList();
    }
}
