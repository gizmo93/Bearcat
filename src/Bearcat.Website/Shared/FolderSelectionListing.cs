namespace Bearcat.Website.Shared;

public sealed record FolderSelectionListing(
    bool IsSuccess,
    IReadOnlyList<string> FolderPaths,
    string? ErrorMessage
)
{
    public static FolderSelectionListing Success(IReadOnlyList<string> folderPaths)
    {
        return new FolderSelectionListing(true, folderPaths, null);
    }

    public static FolderSelectionListing Failure(string? errorMessage)
    {
        return new FolderSelectionListing(false, [], errorMessage);
    }
}
