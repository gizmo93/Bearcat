using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleaseCollections.Results;

public partial class ReleaseCollectionSearchResultList : ComponentBase
{
    public const string GridColumnsClass =
        "grid-cols-[1.75rem_minmax(0,1fr)_auto] xl:grid-cols-[1.75rem_minmax(0,1fr)_8rem_7rem_9rem_4.5rem_3.5rem_6rem_2.25rem]";

    private const int SkeletonRowCount = 8;

    [Parameter]
    public IReadOnlyList<ReleaseCollectionReadModel>? ReleaseCollections { get; set; }

    [Parameter]
    [EditorRequired]
    public SearchResultSelection Selection { get; set; } = null!;

    [Parameter]
    public string? SearchTerm { get; set; }

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public bool HasActiveFilters { get; set; }

    [Parameter]
    public EventCallback OnResetFilters { get; set; }

    [Parameter]
    public EventCallback OnSelectionChanged { get; set; }

    private IReadOnlyList<int> VisibleReleaseCollectionIds =>
        ReleaseCollections
            ?.Select(releaseCollection => releaseCollection.ReleaseCollectionId)
            .ToList()
        ?? [];

    private bool AreAllVisibleReleaseCollectionsSelected =>
        Selection.AreAllSelected(VisibleReleaseCollectionIds);

    private bool AreSomeVisibleReleaseCollectionsSelected =>
        Selection.AreSomeButNotAllSelected(VisibleReleaseCollectionIds);

    private async Task ToggleReleaseCollectionAsync(int releaseCollectionId, bool extendRange)
    {
        Selection.Toggle(VisibleReleaseCollectionIds, releaseCollectionId, extendRange);
        await OnSelectionChanged.InvokeAsync();
    }

    private async Task ToggleAllVisibleReleaseCollectionsAsync()
    {
        if (AreAllVisibleReleaseCollectionsSelected)
        {
            Selection.Clear();
        }
        else
        {
            Selection.SelectAll(VisibleReleaseCollectionIds);
        }

        await OnSelectionChanged.InvokeAsync();
    }
}
