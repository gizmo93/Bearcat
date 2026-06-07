using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageReleases;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

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

    private async Task ShowLatestUploadLinksDialogAsync(ReleaseCollectionReleaseReadModel release)
    {
        if (release.LatestUploadId is null)
        {
            return;
        }

        var uploadConfigName = release.LatestUploadConfigName ?? release.Name;
        var parameters = new Dictionary<string, object?>
        {
            [nameof(UploadLinksDialog.ReleaseId)] = release.ReleaseId,
            [nameof(UploadLinksDialog.UploadId)] = release.LatestUploadId.Value,
            [nameof(UploadLinksDialog.UploadConfigName)] = uploadConfigName,
        };

        await dialogService.OpenAsync<UploadLinksDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["UploadLinksTitle", release.LatestUploadId.Value],
                Description = L["UploadLinksDialogDescription", uploadConfigName],
                Size = DialogSize.Full,
                ShowClose = true,
            }
        );
    }

    private async Task ShowCreateUploadSlotDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateCollectionUploadSlotDialog.FormModel)] = new CollectionUploadSlotFormModel
            {
                ReleaseCollectionId = ReleaseCollectionId,
            },
            [nameof(CreateCollectionUploadSlotDialog.ExistingSlotKeys)] = releaseCollection
                .UploadSlots.Select(slot => slot.Key)
                .ToList(),
        };

        var dialog = await dialogService.OpenAsync<CreateCollectionUploadSlotDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["NewCollectionUploadSlot"],
                Description = L["CreateCollectionUploadSlotDialogDescription"],
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

    private async Task DeleteUploadSlotAsync(CollectionUploadSlotReadModel uploadSlot)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", uploadSlot.Name],
            L[
                "DeleteCollectionUploadSlotConfirmation",
                uploadSlot.Name,
                uploadSlot.UploadConfigCount,
                uploadSlot.UploadCount,
                uploadSlot.Containers.Count
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

        var service = ScopedServices.GetRequiredService<ReleaseCollectionService>();
        await service.DeleteUploadSlotAsync(uploadSlot.CollectionUploadSlotId);
        await LoadReleaseCollectionAsync();
    }

    private static BadgeVariant GetContainerVariant(LinkCrypterContainerState state) =>
        state switch
        {
            LinkCrypterContainerState.Created => BadgeVariant.Default,
            LinkCrypterContainerState.CreationFailed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };
}
