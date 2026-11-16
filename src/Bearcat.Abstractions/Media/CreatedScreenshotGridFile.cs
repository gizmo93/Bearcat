namespace Bearcat.Abstractions.Media;

public sealed record CreatedScreenshotGridFile(
    string FilePath,
    int Width,
    int Height,
    long SizeBytes
);
