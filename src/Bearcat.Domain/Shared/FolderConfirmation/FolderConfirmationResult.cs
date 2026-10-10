namespace Bearcat.Domain.Shared.FolderConfirmation;

public sealed record FolderConfirmationResult(FolderConfirmationState State, string? RootPath)
{
    public bool IsWriteAllowed =>
        State
            is FolderConfirmationState.OutsideConfirmableFolders
                or FolderConfirmationState.Confirmed;
}
