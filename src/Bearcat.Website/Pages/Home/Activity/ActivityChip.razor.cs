using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Activity;

public partial class ActivityChip : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string IconName { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public string Text { get; set; } = null!;

    [Parameter]
    public string? Title { get; set; }
}
