using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class AddReleaseToCollectionDialog(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    [Parameter]
    public int ReleaseCollectionId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private int? selectedReleaseId;
    private string searchQuery = string.Empty;
    private IReadOnlyList<SelectOption<int?>> releaseOptions = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadReleasesAsync(null);
    }

    private async Task OnSearchQueryChangedAsync(string query)
    {
        searchQuery = query;
        await LoadReleasesAsync(query);
    }

    private async Task LoadReleasesAsync(string? searchTerm)
    {
        var releases = await operationRunner.RunAsync(
            (IReleaseCollectionReadRepository repository) =>
                repository.SearchAvailableReleasesAsync(ReleaseCollectionId, searchTerm)
        );

        releaseOptions = releases
            .Select(release => new SelectOption<int?>(release.ReleaseId, release.Name))
            .ToList();
    }

    private async Task OnReleaseSelectedAsync()
    {
        if (selectedReleaseId is null)
        {
            return;
        }

        await operationRunner.RunAsync(
            (ReleaseCollectionService service) =>
                service.AddReleaseAsync(ReleaseCollectionId, selectedReleaseId.Value)
        );
        await DialogRef.CloseAsync(DialogResult.Ok(selectedReleaseId.Value));
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
