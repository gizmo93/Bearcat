using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class ReleaseCollectionDetail(
    IReleaseCollectionReadRepository readRepository,
    DialogService dialogService,
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

    private async Task ShowEditSharedLinkCryptersDialogAsync(
        CollectionUploadSlotReadModel uploadSlot
    )
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(EditCollectionUploadSlotLinkCryptersDialog.CollectionUploadSlotId)] =
                uploadSlot.CollectionUploadSlotId,
            [nameof(EditCollectionUploadSlotLinkCryptersDialog.SlotName)] = uploadSlot.Name,
            [nameof(EditCollectionUploadSlotLinkCryptersDialog.SharedLinkCrypters)] =
                uploadSlot.SharedLinkCrypters,
        };

        var dialog = await dialogService.OpenAsync<EditCollectionUploadSlotLinkCryptersDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditSharedLinkCrypters"],
                Description = L["SharedLinkCryptersDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadReleaseCollectionAsync();
        }
    }

    private static BadgeVariant GetContainerVariant(LinkCrypterContainerState state) =>
        state switch
        {
            LinkCrypterContainerState.Created => BadgeVariant.Default,
            LinkCrypterContainerState.CreationFailed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };
}
