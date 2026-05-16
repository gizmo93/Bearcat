using Bearcat.Domain.UseCases.ManageUploadConfigs;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using Bearcat.Website.Blueprint.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Blueprint.Pages.ManageUploadConfigs;

public partial class UploadConfigs(DialogService dialogService)
    : OwningComponentBase,
        IReloadableComponent
{
    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    private IReadOnlyList<UploadConfigDto> uploadConfigs = [];
    private IUploadConfigReadRepository readRepository = null!;

    protected override async Task OnInitializedAsync()
    {
        readRepository = ScopedServices.GetRequiredService<IUploadConfigReadRepository>();
        await LoadUploadConfigsAsync();
    }

    private async Task ShowAddDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditUploadConfigDialog.ReleaseId)] = ReleaseId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditUploadConfigDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = "Add Upload Config",
                Description = "Define where archives go and keep track of distributed links.",
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadUploadConfigsAsync();
        }
    }

    private async Task ShowEditDialogAsync(UploadConfigDto uploadConfigDto)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditUploadConfigDialog.ReleaseId)] = ReleaseId,
            [nameof(CreateOrEditUploadConfigDialog.UploadConfigId)] =
                uploadConfigDto.UploadConfigId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditUploadConfigDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = "Edit Upload Config",
                Description = "Define where archives go and keep track of distributed links.",
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadUploadConfigsAsync();
        }
    }

    private async Task DeleteConfigAsync(UploadConfigDto uploadConfigDto)
    {
        var result = await dialogService.ConfirmAsync(
            "Delete Upload Config",
            $"Are you sure you want to delete the upload config {uploadConfigDto.Name}?",
            new ConfirmDialogOptions
            {
                ConfirmText = "Delete",
                CancelText = "Cancel",
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        var service = ScopedServices.GetRequiredService<UploadConfigService>();
        await service.DeleteAsync(uploadConfigDto.UploadConfigId);
        await LoadUploadConfigsAsync();
    }

    private async Task LoadUploadConfigsAsync()
    {
        uploadConfigs = await readRepository.GetUploadConfigsAsync(ReleaseId);
    }

    public async Task ReloadAsync()
    {
        await LoadUploadConfigsAsync();
        StateHasChanged();
    }
}
