using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleaseScreenshots;

public partial class ReleaseScreenshotSettingsFields : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public ReleaseScreenshotSettingsFormModel FormModel { get; set; } = null!;
}
