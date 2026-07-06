using Bearcat.Domain.UseCases.ManageLinkCrypterContainers;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageForumPostTemplates;
using Bearcat.Website.Pages.ManageReleases;
using Bearcat.Website.Pages.PostToForum;
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

    [SupplyParameterFromQuery(Name = "workflow")]
    public string? Workflow { get; set; }

    private ReleaseCollectionDetailReadModel releaseCollection = null!;
    private bool isInitialized;
    private bool isResolvingMetadata;
    private int? loadedReleaseCollectionId;

    private bool IsPostQueueWorkflow =>
        string.Equals(Workflow, "postqueue", StringComparison.OrdinalIgnoreCase);

    protected override async Task OnParametersSetAsync()
    {
        if (loadedReleaseCollectionId != ReleaseCollectionId)
        {
            await LoadReleaseCollectionAsync();
        }
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
        loadedReleaseCollectionId = ReleaseCollectionId;
        isInitialized = true;
    }

    private async Task ShowEditContentTypeDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(EditReleaseCollectionContentTypeDialog.ReleaseCollectionId)] =
                ReleaseCollectionId,
            [nameof(EditReleaseCollectionContentTypeDialog.ContentType)] =
                releaseCollection.ReleaseContentType,
        };

        var dialog = await dialogService.OpenAsync<EditReleaseCollectionContentTypeDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["ReleaseContentType"],
                Description = L["EditContentTypeDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        toastService.Success(L["ReleaseContentTypeUpdated"]);
        await LoadReleaseCollectionAsync();
    }

    private async Task ShowEditMetadataDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(EditCollectionMetadataDialog.ReleaseCollectionId)] = ReleaseCollectionId,
            [nameof(EditCollectionMetadataDialog.CollectionName)] = releaseCollection.Name,
            [nameof(EditCollectionMetadataDialog.Metadata)] = releaseCollection.Metadata,
        };

        var dialog = await dialogService.OpenAsync<EditCollectionMetadataDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditMetadata"],
                Description = L["EditMetadataDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        toastService.Success(L["MetadataUpdated"]);
        await LoadReleaseCollectionAsync();
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

    private async Task ShowPostToForumDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(PostToForumDialog.EntityId)] = ReleaseCollectionId,
            [nameof(PostToForumDialog.EntityName)] = releaseCollection.Name,
            [nameof(PostToForumDialog.TemplateType)] = ForumPostTemplateType.ReleaseCollection,
        };

        await dialogService.OpenAsync<PostToForumDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["PostNamedCollectionToForum", releaseCollection.Name],
                Description = L["PostToForumDescription"],
                Size = DialogSize.ExtraLarge,
                ShowClose = true,
                PreventClose = true,
            }
        );
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

    private async Task DeleteFailedContainerAsync(CollectionUploadSlotContainerReadModel container)
    {
        if (container.State != LinkCrypterContainerState.CreationFailed)
        {
            return;
        }

        var result = await dialogService.ConfirmAsync(
            L["DeleteLinkCrypterContainer"],
            L["DeleteLinkCrypterContainerConfirmation"],
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

        var service = ScopedServices.GetRequiredService<LinkCrypterContainerService>();
        await service.DeleteFailedContainerAsync(
            container.LinkCrypterContainerId,
            CancellationToken.None
        );
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
