using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Activity;

public partial class ActivityLaneHeading : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string IconName { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public int Count { get; set; }
}
