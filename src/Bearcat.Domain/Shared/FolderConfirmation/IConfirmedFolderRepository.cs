namespace Bearcat.Domain.Shared.FolderConfirmation;

public interface IConfirmedFolderRepository
{
    Task<Guid?> GetMarkerIdAsync(string path, CancellationToken cancellationToken);

    Task<bool> AnyUnresolvedFolderNotConfirmedNotificationAsync(
        string message,
        CancellationToken cancellationToken
    );
}
