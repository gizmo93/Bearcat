using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Summary;

public partial class ActivitySummaryTile : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string IconName { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = null!;

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public string? SubLabel { get; set; }

    [Parameter]
    public TransferSpeedHistory? SpeedHistory { get; set; }
}
