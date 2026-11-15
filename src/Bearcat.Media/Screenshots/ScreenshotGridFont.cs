namespace Bearcat.Media.Screenshots;

public static class ScreenshotGridFont
{
    public const string FileName = "NotoSans-Regular.ttf";

    private const string ResourceName = "Bearcat.Media.Fonts.NotoSans-Regular.ttf";

    public static async Task WriteToDirectoryAsync(
        string directory,
        CancellationToken cancellationToken
    )
    {
        await using var resourceStream =
            typeof(ScreenshotGridFont).Assembly.GetManifestResourceStream(ResourceName)!;
        await using var fileStream = File.Create(Path.Combine(directory, FileName));
        await resourceStream.CopyToAsync(fileStream, cancellationToken);
    }
}
