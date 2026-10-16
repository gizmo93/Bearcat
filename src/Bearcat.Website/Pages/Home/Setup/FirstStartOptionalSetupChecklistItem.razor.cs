using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Setup;

public partial class FirstStartOptionalSetupChecklistItem : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string IconName { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public string Description { get; set; } = null!;

    [Parameter]
    public bool IsDone { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
