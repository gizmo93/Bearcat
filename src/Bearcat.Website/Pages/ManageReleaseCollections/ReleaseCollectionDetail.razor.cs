using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageForumPostTemplates;
using Bearcat.Website.Pages.ManageReleases;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class ReleaseCollectionDetail(
    IReleaseCollectionReadRepository readRepository,
    DialogService dialogService,
    ToastService toastService,
    NavigationManager navigationManager
)
{
    [Parameter]
    public int ReleaseCollectionId { get; set; }

    private ReleaseCollectionDetailReadModel releaseCollection = null!;
    private bool isInitialized;
    private bool isResolvingMetadata;

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

    private async Task ResolveMetadataAsync()
    {
        if (isResolvingMetadata)
        {
            return;
        }

        isResolvingMetadata = true;

        try
        {
            var service =
                ScopedServices.GetRequiredService<ReleaseCollectionInfoResolutionService>();
            var resolved = await service.ResolveAsync(ReleaseCollectionId);

            if (resolved)
            {
                toastService.Success(L["SeriesMetadataResolved"]);
                await LoadReleaseCollectionAsync();
            }
            else
            {
                toastService.Info(L["SeriesMetadataNotResolved"]);
            }
        }
        finally
        {
            isResolvingMetadata = false;
        }
    }

    private async Task ShowRenderForumPostDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(RenderForumPostDialog.EntityId)] = ReleaseCollectionId,
            [nameof(RenderForumPostDialog.Type)] = ForumPostTemplateType.ReleaseCollection,
        };

        await dialogService.OpenAsync<RenderForumPostDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["RenderForumPostForCollection", releaseCollection.Name],
                Description = L["RenderForumPostDescription"],
                Size = DialogSize.Full,
                ShowClose = true,
            }
        );
    }

    private async Task ShowUploadLinksDialogAsync(
        ReleaseCollectionReleaseReadModel release,
        ReleaseLatestUploadReadModel upload
    )
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(UploadLinksDialog.ReleaseId)] = release.ReleaseId,
            [nameof(UploadLinksDialog.UploadId)] = upload.UploadId,
            [nameof(UploadLinksDialog.UploadConfigName)] = upload.UploadConfigName,
        };

        await dialogService.OpenAsync<UploadLinksDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["UploadLinksTitle", upload.UploadId],
                Description = L["UploadLinksDialogDescription", upload.UploadConfigName],
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

    private async Task ShowAddReleaseDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(AddReleaseToCollectionDialog.ReleaseCollectionId)] = ReleaseCollectionId,
        };

        var dialog = await dialogService.OpenAsync<AddReleaseToCollectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["AddRelease"],
                Description = L["AddReleaseToCollectionDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadReleaseCollectionAsync();
        }
    }

    private async Task RemoveReleaseAsync(ReleaseCollectionReleaseReadModel release)
    {
        var result = await dialogService.ConfirmAsync(
            L["RemoveFromCollection"],
            L["RemoveReleaseFromCollectionConfirmation", release.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Remove"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        var service = ScopedServices.GetRequiredService<ReleaseCollectionService>();
        await service.RemoveReleaseAsync(ReleaseCollectionId, release.ReleaseId);
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
