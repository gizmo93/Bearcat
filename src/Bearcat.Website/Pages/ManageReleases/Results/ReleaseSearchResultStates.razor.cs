using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseSearchResultStates : ComponentBase
{
    [Parameter]
    public IReadOnlyList<ReleaseSearchResultReadModel>? Releases { get; set; }

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public bool HasActiveFilters { get; set; }

    [Parameter]
    public EventCallback OnResetFilters { get; set; }

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
