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
}
