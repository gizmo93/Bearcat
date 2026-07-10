using Bearcat.Domain.UseCases.ManageImageUploadConfigs;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs.ReadModels;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs.Repositories;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageImageUploadConfigs;

public partial class ImageUploadConfigs(
    DialogService dialogService,
    IScopedOperationRunner operationRunner
) : ComponentBase, IReloadableComponent
{
    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    private IReadOnlyList<ImageUploadConfigReadModel> imageUploadConfigs = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadImageUploadConfigsAsync();
    }

    private async Task ShowAddDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditImageUploadConfigDialog.ReleaseId)] = ReleaseId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditImageUploadConfigDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["AddImageUploadConfig"],
                Description = L["ImageUploadConfigDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadImageUploadConfigsAsync();
        }
    }

    private async Task ShowEditDialogAsync(ImageUploadConfigReadModel config)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditImageUploadConfigDialog.ReleaseId)] = ReleaseId,
            [nameof(CreateOrEditImageUploadConfigDialog.ImageUploadConfigId)] =
                config.ImageUploadConfigId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditImageUploadConfigDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditImageUploadConfig"],
                Description = L["ImageUploadConfigDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadImageUploadConfigsAsync();
        }
    }

    private async Task DeleteConfigAsync(ImageUploadConfigReadModel config)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteImageUploadConfig"],
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
        await LoadImageUploadConfigsAsync();
    }

    private async Task LoadImageUploadConfigsAsync()
    {
        imageUploadConfigs = await operationRunner.RunAsync(
            (IImageUploadConfigReadRepository repository) =>
                repository.GetImageUploadConfigsAsync(ReleaseId)
        );
    }

    public async Task ReloadAsync()
    {
        await LoadImageUploadConfigsAsync();
        StateHasChanged();
    }
}
