namespace Bearcat.Domain.UseCases.ManageReleases;

public static class MediaFileTypes
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv",
        ".mp4",
        ".avi",
        ".m2ts",
        ".ts",
        ".mov",
        ".wmv",
        ".flv",
        ".mpg",
        ".mpeg",
        ".webm",
    };

    public static bool IsVideoFile(string filePath)
    {
        return VideoExtensions.Contains(Path.GetExtension(filePath));
    }
}
