namespace Bearcat.Domain.UseCases.ManageReleaseScreenshots;

public sealed record ReleaseScreenshotSettings(
    bool CreateScreenshots,
    int ScreenshotCount,
    bool CreateScreenshotGrid,
    int ScreenshotGridColumns,
    int ScreenshotGridRows
);
