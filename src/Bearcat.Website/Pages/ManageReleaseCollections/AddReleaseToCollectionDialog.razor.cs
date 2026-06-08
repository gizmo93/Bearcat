using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class AddReleaseToCollectionDialog(IReleaseCollectionReadRepository readRepository)
    : OwningComponentBase
{
    [Parameter]
    public int ReleaseCollectionId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private int? selectedReleaseId;
    private string searchQuery = string.Empty;
    private IEnumerable<SelectOption<int?>> releaseOptions = [];

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
        var releases = await readRepository.SearchAvailableReleasesAsync(
            ReleaseCollectionId,
            searchTerm
        );

        releaseOptions = releases.Select(release => new SelectOption<int?>(
            release.ReleaseId,
            release.Name
        ));
    }

    private async Task OnReleaseSelectedAsync()
    {
        if (selectedReleaseId is null)
        {
            return;
        }

        var service = ScopedServices.GetRequiredService<ReleaseCollectionService>();
        await service.AddReleaseAsync(ReleaseCollectionId, selectedReleaseId.Value);
        await DialogRef.CloseAsync(DialogResult.Ok(selectedReleaseId.Value));
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
