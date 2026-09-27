using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home;

public partial class TransferProgressCell : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public double Percentage { get; set; }

    [Parameter]
    public string? Speed { get; set; }

    [Parameter]
    [EditorRequired]
    public bool IsExpanded { get; set; }

    [Parameter]
    [EditorRequired]
    public string ToggleAriaLabel { get; set; } = null!;

    [Parameter]
    public bool ToggleDisabled { get; set; }

    [Parameter]
    [EditorRequired]
    public EventCallback OnToggleDetails { get; set; }
}
