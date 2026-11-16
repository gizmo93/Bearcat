namespace Bearcat.Abstractions.Media;

public sealed record ScreenshotCreationRequest(
    string VideoFilePath,
    TimeSpan Duration,
    VideoTransferCharacteristic TransferCharacteristic,
    int Count,
    string OutputDirectory
);
