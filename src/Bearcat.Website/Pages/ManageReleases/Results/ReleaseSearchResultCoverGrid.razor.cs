using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseSearchResultCoverGrid : ComponentBase
{
    private const int SkeletonCardCount = 12;

    [Parameter]
    public IReadOnlyList<ReleaseSearchResultReadModel>? Releases { get; set; }

    [Parameter]
    [EditorRequired]
    public ReleaseSelection Selection { get; set; } = null!;

    [Parameter]
    public string? SearchTerm { get; set; }

    [Parameter]
    public int? FocusedReleaseId { get; set; }

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public bool HasActiveFilters { get; set; }

    [Parameter]
    public EventCallback OnResetFilters { get; set; }

    [Parameter]
    public EventCallback OnSelectionChanged { get; set; }

    [Parameter]
    public EventCallback<ReleaseSearchResultReadModel> OnQuickLook { get; set; }

    private IReadOnlyList<int> VisibleReleaseIds =>
        Releases?.Select(release => release.ReleaseId).ToList() ?? [];

    private async Task ToggleReleaseAsync(int releaseId, bool extendRange)
    {
        Selection.Toggle(VisibleReleaseIds, releaseId, extendRange);
        await OnSelectionChanged.InvokeAsync();
    }
}
