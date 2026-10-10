using Bearcat.Domain.Shared.FolderConfirmation;

namespace Bearcat.Domain.UseCases.ManageConfirmedFolders.ReadModels;

public sealed record ConfirmableFolderReadModel(
    string Path,
    FolderConfirmationState State,
    bool Exists
);
