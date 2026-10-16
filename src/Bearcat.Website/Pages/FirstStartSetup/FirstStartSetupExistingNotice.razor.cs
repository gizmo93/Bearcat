using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupExistingNotice : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string Text { get; set; } = null!;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
