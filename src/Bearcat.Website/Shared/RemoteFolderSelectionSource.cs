using Bearcat.Domain.UseCases.ManageRemoteSourceAutomations;
using Bearcat.Domain.UseCases.ManageRemoteSources;
using Bearcat.Website.ScopedOperations;

namespace Bearcat.Website.Shared;

public sealed class RemoteFolderSelectionSource(
    IScopedOperationRunner operationRunner,
    int remoteSourceRegistrationId
) : IFolderSelectionSource
{
    private const char Separator = '/';
    private const string RootPath = "/";

    public IReadOnlyList<string> RootPaths { get; } = [RootPath];

    public async Task<FolderSelectionListing> GetChildEntriesAsync(string path)
    {
        var result = await operationRunner.RunAsync(
            (RemoteSourceRegistrationService service) =>
                service.ListFoldersAsync(remoteSourceRegistrationId, path)
        );

        return result.IsSuccess
            ? FolderSelectionListing.Success(
                result
                    .Folders.Select(folder => RemotePathNormalizer.Normalize(folder.FullPath))
                    .ToList()
            )
            : FolderSelectionListing.Failure(result.ErrorMessage);
    }

    public string? NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : RemotePathNormalizer.Normalize(path);
    }

    public string? GetParentPath(string path)
    {
        var normalizedPath = NormalizePath(path);

        if (normalizedPath is null || normalizedPath == RootPath)
        {
            return null;
        }

        var separatorIndex = normalizedPath.LastIndexOf(Separator);

        return separatorIndex == 0 ? RootPath : normalizedPath[..separatorIndex];
    }

    public bool IsSameOrDescendantPath(string path, string ancestorPath)
    {
        var normalizedPath = NormalizePath(path);
        var normalizedAncestorPath = NormalizePath(ancestorPath);

        if (normalizedPath is null || normalizedAncestorPath is null)
        {
            return false;
        }

        if (normalizedPath == normalizedAncestorPath || normalizedAncestorPath == RootPath)
        {
            return true;
        }

        return normalizedPath.StartsWith(
            normalizedAncestorPath + Separator,
            StringComparison.Ordinal
        );
    }

    public string GetDisplayName(string path)
    {
        var normalizedPath = NormalizePath(path);

        if (normalizedPath is null || normalizedPath == RootPath)
        {
            return RootPath;
        }

        return normalizedPath[(normalizedPath.LastIndexOf(Separator) + 1)..];
    }
}
