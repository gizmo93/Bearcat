namespace Bearcat.Abstractions.Media;

public sealed record CreatedScreenshotFile(
    string FilePath,
    TimeSpan Timestamp,
    int Width,
    int Height,
    long SizeBytes
);
