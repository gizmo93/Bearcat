using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupWelcomeStep : ComponentBase
{
    [Parameter]
    public EventCallback OnStart { get; set; }

    [Parameter]
    public EventCallback OnSkipSetup { get; set; }
}
