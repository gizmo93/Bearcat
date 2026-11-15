using Bearcat.Domain.UseCases.ManageReleaseScreenshots;

namespace Bearcat.Website.Pages.ManageReleaseScreenshots;

public class ReleaseScreenshotSettingsFormModel
{
    public bool CreateScreenshots { get; set; }

    public int ScreenshotCount { get; set; } = 6;

    public bool CreateScreenshotGrid { get; set; }

    public int ScreenshotGridColumns { get; set; } = 4;

    public int ScreenshotGridRows { get; set; } = 4;

    public ReleaseScreenshotSettings ToSettings()
    {
        return new ReleaseScreenshotSettings(
            CreateScreenshots: CreateScreenshots,
            ScreenshotCount: ScreenshotCount,
            CreateScreenshotGrid: CreateScreenshotGrid,
            ScreenshotGridColumns: ScreenshotGridColumns,
            ScreenshotGridRows: ScreenshotGridRows
        );
    }
}
