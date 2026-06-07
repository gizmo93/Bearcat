using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class ReleaseCollectionDetail(
    IReleaseCollectionReadRepository readRepository,
    NavigationManager navigationManager
)
{
    [Parameter]
    public int ReleaseCollectionId { get; set; }

    private ReleaseCollectionDetailReadModel releaseCollection = null!;
    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        await LoadReleaseCollectionAsync();
    }

    private async Task LoadReleaseCollectionAsync()
    {
        var detail = await readRepository.GetDetailAsync(ReleaseCollectionId);

        if (detail is null)
        {
            navigationManager.NotFound();
            return;
        }

        releaseCollection = detail;
        isInitialized = true;
    }
}
