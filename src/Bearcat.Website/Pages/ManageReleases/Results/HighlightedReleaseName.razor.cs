using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class HighlightedReleaseName : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string Name { get; set; } = string.Empty;

    [Parameter]
    public string? SearchTerm { get; set; }
}
