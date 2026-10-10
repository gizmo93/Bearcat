namespace Bearcat.Domain.UseCases.ManageConfirmedFolders.ReadModels;

public sealed record FolderTopLevelEntriesReadModel(
    IReadOnlyList<FolderTopLevelEntryReadModel> Entries,
    int TotalEntryCount
);
