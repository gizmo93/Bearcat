namespace Bearcat.Domain.Shared.FolderConfirmation;

public enum FolderConfirmationState
{
    OutsideConfirmableFolders = 1,
    Confirmed = 2,
    NotConfirmed = 3,
    MarkerMissing = 4,
}
