using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;

public static class RemoteDownloadPaths
{
    public static string? GetSafeLocalFilePath(string localFolderPath, string relativePath)
    {
        var segments = relativePath.Split('/');

        if (!segments.All(RemoteFolderNameValidator.IsSafe))
        {
            return null;
        }

        var folderPath = Path.GetFullPath(localFolderPath);
        var filePath = Path.GetFullPath(Path.Combine([folderPath, .. segments]));

        return IsPathInside(filePath, folderPath) ? filePath : null;
    }

    public static bool CanDeleteDownloadFolder(RemoteSourceDownload download)
    {
        if (!RemoteFolderNameValidator.IsSafe(download.FolderName))
        {
            return false;
        }

        var folderPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(download.LocalFolderPath)
        );

        return Path.GetFileName(folderPath) == download.FolderName;
    }

    private static bool IsPathInside(string path, string parentPath)
    {
        var relativePath = Path.GetRelativePath(parentPath, path);

        return relativePath != "."
            && relativePath != ".."
            && !relativePath.StartsWith(
                ".." + Path.DirectorySeparatorChar,
                StringComparison.Ordinal
            )
            && !relativePath.StartsWith(
                ".." + Path.AltDirectorySeparatorChar,
                StringComparison.Ordinal
            )
            && !Path.IsPathRooted(relativePath);
    }
}
