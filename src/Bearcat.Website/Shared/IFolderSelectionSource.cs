namespace Bearcat.Website.Shared;

public interface IFolderSelectionSource
{
    IReadOnlyList<string> RootPaths { get; }

    Task<FolderSelectionListing> GetChildFoldersAsync(string path);

    string? NormalizePath(string? path);

    string? GetParentPath(string path);

    bool IsSameOrDescendantPath(string path, string ancestorPath);

    string GetDisplayName(string path);
}
