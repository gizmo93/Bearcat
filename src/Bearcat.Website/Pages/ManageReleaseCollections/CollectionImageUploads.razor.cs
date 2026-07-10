using Bearcat.Domain.UseCases.ManageImageUploadConfigs;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class CollectionImageUploads(
    IScopedOperationRunner operationRunner,
    DialogService dialogService
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public int ReleaseCollectionId { get; set; }

    private IReadOnlyList<CollectionImageUploadReadModel> imageUploads = [];
    private bool isLoading;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        isLoading = true;

        try
        {
            imageUploads = await operationRunner.RunAsync(
                (IReleaseCollectionReadRepository repository) =>
                    repository.GetImageUploadsAsync(ReleaseCollectionId)
            );
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ShowAddDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditCollectionImageUploadConfigDialog.ReleaseCollectionId)] =
                ReleaseCollectionId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditCollectionImageUploadConfigDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["AddImageUploadConfig"],
                Description = L["CollectionImageUploadsDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await RefreshAsync();
        }
    }

    private async Task ShowEditDialogAsync(CollectionImageUploadReadModel config)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditCollectionImageUploadConfigDialog.ReleaseCollectionId)] =
                ReleaseCollectionId,
            [nameof(CreateOrEditCollectionImageUploadConfigDialog.ImageUploadConfigId)] =
                config.ImageUploadConfigId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditCollectionImageUploadConfigDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", config.Name],
                Description = L["CollectionImageUploadsDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await RefreshAsync();
        }
    }

    private async Task DeleteConfigAsync(CollectionImageUploadReadModel config)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", config.Name],
            L["DeleteImageUploadConfigConfirmation", config.Name],
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

        await operationRunner.RunAsync(
            (ImageUploadConfigService service) => service.DeleteAsync(config.ImageUploadConfigId)
        );

        await RefreshAsync();
    }

    private static string GetImageUrlsCopyTargetId(CollectionImageUploadReadModel config) =>
        $"collection-image-urls-{config.ImageUploadConfigId}-{config.ImageUploadId}";

    private static string GetImageUrlCopyTargetId(
        CollectionImageUploadReadModel config,
        CollectionImageUploadUrlReadModel imageUrl
    ) =>
        $"collection-image-url-{config.ImageUploadConfigId}-{config.ImageUploadId}-{imageUrl.ImageSize}";

    private static string GetImageUrlsText(CollectionImageUploadReadModel config) =>
        string.Join(Environment.NewLine, config.ImageUrls.Select(url => url.Url));

    private static BadgeVariant GetUploadVariant(UploadState state)
    {
        return state switch
        {
            UploadState.Completed => BadgeVariant.Default,
            UploadState.Failed or UploadState.Canceled => BadgeVariant.Destructive,
            UploadState.Uploading or UploadState.Pending => BadgeVariant.Secondary,
            _ => BadgeVariant.Outline,
        };
    }
}
