using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Activity;

public partial class ActivityFileRow : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string FileName { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public double Percentage { get; set; }

    [Parameter]
    public string? ProxyServerName { get; set; }

    [Parameter]
    public string? ProgressIndicatorClass { get; set; }

    [Parameter]
    [EditorRequired]
    public RenderFragment Status { get; set; } = null!;
}
