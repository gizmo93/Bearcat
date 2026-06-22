namespace Bearcat.Media;

internal static class MediaInfoBinary
{
    private const string BundledFolderName = "mediainfo";

    public static string Resolve(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        return FindBundled() ?? "mediainfo";
    }

    private static string? FindBundled()
    {
        var fileName = OperatingSystem.IsWindows() ? "MediaInfo.exe" : "mediainfo";
        var bundledPath = Path.Combine(AppContext.BaseDirectory, BundledFolderName, fileName);
        return File.Exists(bundledPath) ? bundledPath : null;
    }
}
