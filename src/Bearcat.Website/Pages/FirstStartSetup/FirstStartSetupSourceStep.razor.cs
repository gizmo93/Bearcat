using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.FirstStartSetup;

public partial class FirstStartSetupSourceStep : ComponentBase
{
    [Parameter]
    public EventCallback OnBack { get; set; }
}
