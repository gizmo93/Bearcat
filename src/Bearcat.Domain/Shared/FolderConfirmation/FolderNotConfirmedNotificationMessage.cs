namespace Bearcat.Domain.Shared.FolderConfirmation;

public static class FolderNotConfirmedNotificationMessage
{
    public static string Get(FolderConfirmationResult result)
    {
        return result.State switch
        {
            FolderConfirmationState.NotConfirmed => GetNotConfirmedMessage(result.RootPath!),
            FolderConfirmationState.MarkerMissing => GetMarkerMissingMessage(result.RootPath!),
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(result),
                actualValue: result.State,
                message: "Only folders that block writing have a notification message."
            ),
        };
    }

    public static IReadOnlyList<string> GetAll(string rootPath)
    {
        return [GetNotConfirmedMessage(rootPath), GetMarkerMissingMessage(rootPath)];
    }

    private static string GetNotConfirmedMessage(string rootPath)
    {
        return $"The folder {rootPath} must be confirmed on the folders page before Bearcat writes into it. Downloads, archive creation, restores and deletions in this folder are paused until then.";
    }

    private static string GetMarkerMissingMessage(string rootPath)
    {
        return $"The folder {rootPath} was confirmed, but its marker file {FolderConfirmationMarkerFile.FileName} is missing or does not match. The folder is probably not mounted. Check the mount instead of confirming the folder again. Downloads, archive creation, restores and deletions in this folder are paused until then.";
    }
}
