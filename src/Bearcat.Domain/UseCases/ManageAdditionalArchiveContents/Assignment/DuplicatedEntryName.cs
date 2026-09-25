namespace Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Assignment;

public record DuplicatedEntryName(
    string EntryName,
    IReadOnlyList<string> AdditionalArchiveContentNames
);
