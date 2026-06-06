using Bearcat.Domain.UseCases.ManageImageUploads.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageImageUploads;

public partial class ReleaseImageUploads(
    IServiceScopeFactory serviceScopeFactory,
    DialogService dialogService
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    private IReadOnlyList<ReleaseImageUploadReadModel> imageUploads = [];
    private bool isLoading;

    protected override async Task OnInitializedAsync()
    {
        await RefreshImageUploadsAsync();
    }

    private async Task RefreshImageUploadsAsync()
    {
        isLoading = true;

        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var readRepository = scope.ServiceProvider.GetRequiredService<IReleaseReadRepository>();
            imageUploads = await readRepository.GetImageUploadsAsync(ReleaseId);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ShowUrlsDialogAsync(ReleaseImageUploadReadModel upload)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ImageUploadUrlsDialog.ReleaseId)] = ReleaseId,
            [nameof(ImageUploadUrlsDialog.ImageUploadId)] = upload.ImageUploadId,
        };

        await dialogService.OpenAsync<ImageUploadUrlsDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["ImageUploadUrls"],
                Description = upload.ImageUploadConfigName,
                Size = DialogSize.ExtraLarge,
                ShowClose = true,
            }
        );
    }

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
