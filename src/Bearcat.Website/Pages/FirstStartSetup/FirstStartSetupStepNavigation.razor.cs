using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupStepNavigation : ComponentBase
{
    [Parameter]
    public EventCallback OnBack { get; set; }

    [Parameter]
    public EventCallback OnSkip { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
