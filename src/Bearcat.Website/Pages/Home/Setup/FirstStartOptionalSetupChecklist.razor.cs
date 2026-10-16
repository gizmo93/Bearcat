using Bearcat.Domain.UseCases.FirstStartSetup.Status;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Setup;

public partial class FirstStartOptionalSetupChecklist : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public FirstStartSetupDatabaseStatus SetupStatus { get; set; } = null!;

    [Parameter]
    public EventCallback OnHide { get; set; }
}
