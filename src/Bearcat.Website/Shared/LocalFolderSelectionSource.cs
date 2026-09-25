using Bearcat.Abstractions;
using Bearcat.Website.ScopedOperations;

namespace Bearcat.Website.Shared;

public sealed class LocalFolderSelectionSource(
    IScopedOperationRunner operationRunner,
    IReadOnlyList<string> rootPaths,
    bool includeFiles = false
) : IFolderSelectionSource
{
    public IReadOnlyList<string> RootPaths { get; } = rootPaths;

    public Task<FolderSelectionListing> GetChildEntriesAsync(string path)
    {
        try
        {
            var (folderPaths, filePaths) = operationRunner.Run(
                (IFileSystemService service) =>
                    (
                        service.GetFoldersInPath(path),
                        includeFiles ? service.GetFilesInPath(path, recursive: false) : []
                    )
            );

            return Task.FromResult(FolderSelectionListing.Success(folderPaths, filePaths));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(FolderSelectionListing.Failure(exception.Message));
        }
    }

    public string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetPathRoot(fullPath);

        return string.Equals(fullPath, rootPath, StringComparison.Ordinal)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public string? GetParentPath(string path)
    {
        var parentPath = Path.GetDirectoryName(NormalizePath(path));

        return string.IsNullOrWhiteSpace(parentPath) ? null : parentPath;
    }

    public bool IsSameOrDescendantPath(string path, string ancestorPath)
    {
        var normalizedPath = NormalizePath(path);
        var normalizedAncestorPath = NormalizePath(ancestorPath);

        if (normalizedPath is null || normalizedAncestorPath is null)
        {
            return false;
        }

        if (string.Equals(normalizedPath, normalizedAncestorPath, StringComparison.Ordinal))
        {
            return true;
        }

        var ancestorPrefix = normalizedAncestorPath.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedAncestorPath
            : $"{normalizedAncestorPath}{Path.DirectorySeparatorChar}";

        return normalizedPath.StartsWith(ancestorPrefix, StringComparison.Ordinal);
    }

    public string GetDisplayName(string path)
    {
        var name = Path.GetFileName(path);

        return string.IsNullOrWhiteSpace(name) ? path : name;
    }
}
