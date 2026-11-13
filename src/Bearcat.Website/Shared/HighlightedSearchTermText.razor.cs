using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class HighlightedSearchTermText : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string Text { get; set; } = string.Empty;

    [Parameter]
    public string? SearchTerm { get; set; }
}
