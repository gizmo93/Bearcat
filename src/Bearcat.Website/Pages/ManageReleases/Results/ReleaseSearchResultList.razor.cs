using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseSearchResultList : ComponentBase
{
    public const string GridColumnsClass =
        "grid-cols-[1.75rem_minmax(0,1fr)_auto] xl:grid-cols-[1.75rem_minmax(0,1fr)_8rem_7rem_3.5rem_6rem_6.75rem]";

    private const int SkeletonRowCount = 8;

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

    [Parameter]
    public EventCallback<ReleaseSearchResultReadModel> OnEditRelease { get; set; }

    [Parameter]
    public EventCallback<ReleaseSearchResultReadModel> OnDeleteRelease { get; set; }

    private IReadOnlyList<int> VisibleReleaseIds =>
        Releases?.Select(release => release.ReleaseId).ToList() ?? [];

    private bool AreAllVisibleReleasesSelected => Selection.AreAllSelected(VisibleReleaseIds);

    private bool AreSomeVisibleReleasesSelected =>
        Selection.AreSomeButNotAllSelected(VisibleReleaseIds);

    private async Task ToggleReleaseAsync(int releaseId, bool extendRange)
    {
        Selection.Toggle(VisibleReleaseIds, releaseId, extendRange);
        await OnSelectionChanged.InvokeAsync();
    }

    private async Task ToggleAllVisibleReleasesAsync()
    {
        if (AreAllVisibleReleasesSelected)
        {
            Selection.Clear();
        }
        else
        {
            Selection.SelectAll(VisibleReleaseIds);
        }

        await OnSelectionChanged.InvokeAsync();
    }
}
