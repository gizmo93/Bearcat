namespace Bearcat.Abstractions.Media;

public sealed record ScreenshotGridCreationRequest(
    string VideoFilePath,
    TimeSpan Duration,
    VideoTransferCharacteristic TransferCharacteristic,
    int Columns,
    int Rows,
    int GridWidthPixels,
    IReadOnlyList<string> HeaderLines,
    string OutputFilePath
);
