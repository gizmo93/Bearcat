using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class SearchResultStates<TItem> : ComponentBase
{
    [Parameter]
    public IReadOnlyList<TItem>? Items { get; set; }

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public bool HasActiveFilters { get; set; }

    [Parameter]
    public EventCallback OnResetFilters { get; set; }

    [Parameter]
    [EditorRequired]
    public string NoResultsText { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public string NoMatchingResultsText { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public string NoMatchingResultsHintText { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public string ContainerClass { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public RenderFragment SkeletonContent { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public RenderFragment ChildContent { get; set; } = null!;

    private string ResultsClass => IsLoading ? $"{ContainerClass} opacity-60" : ContainerClass;
}
