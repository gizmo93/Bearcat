namespace Bearcat.Domain.UseCases.ManageReleases;

internal static class FolderPathHelper
{
    public static string GetFolderName(string folderPath)
    {
        var normalizedPath = folderPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );
        return Path.GetFileName(normalizedPath);
    }

    public static bool IsSameOrSubPath(string? childPath, string? parentPath)
    {
        if (string.IsNullOrEmpty(childPath) || string.IsNullOrEmpty(parentPath))
        {
            return false;
        }

        var parent = parentPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );
        var child = childPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(child, parent, StringComparison.Ordinal)
            || child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || child.StartsWith(parent + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }
}
