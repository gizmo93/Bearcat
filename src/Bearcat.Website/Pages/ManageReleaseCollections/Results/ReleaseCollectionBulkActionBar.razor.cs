using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleaseCollections.Results;

public partial class ReleaseCollectionBulkActionBar(
    IScopedOperationRunner operationRunner,
    ToastService toastService,
    DialogService dialogService
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public SearchResultSelection Selection { get; set; } = null!;

    [Parameter]
    public IReadOnlyList<ReleaseCollectionReadModel> VisibleReleaseCollections { get; set; } = [];

    [Parameter]
    public EventCallback OnSelectionChanged { get; set; }

    [Parameter]
    public EventCallback OnReleaseCollectionsDeleted { get; set; }

    private IReadOnlyList<int> VisibleReleaseCollectionIds =>
        VisibleReleaseCollections
            .Select(releaseCollection => releaseCollection.ReleaseCollectionId)
            .ToList();

    private async Task SelectAllOnPageAsync()
    {
        Selection.SelectAll(VisibleReleaseCollectionIds);
        await OnSelectionChanged.InvokeAsync();
    }

    private async Task ClearSelectionAsync()
    {
        Selection.Clear();
        await OnSelectionChanged.InvokeAsync();
    }

    private async Task DeleteSelectedReleaseCollectionsAsync()
    {
        var selectedReleaseCollections = VisibleReleaseCollections
            .Where(releaseCollection => Selection.IsSelected(releaseCollection.ReleaseCollectionId))
            .ToList();

        var releaseCollectionsWithoutReleases = selectedReleaseCollections
            .Where(releaseCollection => releaseCollection.ReleaseCount == 0)
            .ToList();

        var releaseCollectionsWithReleasesCount =
            selectedReleaseCollections.Count - releaseCollectionsWithoutReleases.Count;

        if (releaseCollectionsWithoutReleases.Count == 0)
        {
            toastService.Error(L["DeleteSelectedReleaseCollectionsAllHaveReleases"]);
            return;
        }

        var result = await dialogService.ConfirmAsync(
            L["DeleteSelectedReleaseCollections"],
            releaseCollectionsWithReleasesCount == 0
                ? L[
                    "DeleteSelectedReleaseCollectionsConfirmation",
                    releaseCollectionsWithoutReleases.Count
                ]
                : L[
                    "DeleteSelectedReleaseCollectionsConfirmationWithSkipped",
                    releaseCollectionsWithoutReleases.Count,
                    releaseCollectionsWithReleasesCount
                ],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        foreach (var releaseCollection in releaseCollectionsWithoutReleases)
        {
            await operationRunner.RunAsync(
                (ReleaseCollectionService service) =>
                    service.DeleteAsync(releaseCollection.ReleaseCollectionId)
            );
        }

        toastService.Success(
            L["ReleaseCollectionsDeleted", releaseCollectionsWithoutReleases.Count]
        );

        Selection.Clear();
        await OnReleaseCollectionsDeleted.InvokeAsync();
    }
}
