using Bearcat.Domain.UseCases.ManageUploadConfigs;
using Bearcat.Domain.UseCases.ManageUploadConfigs.ReadModels;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageUploadConfigs;

public partial class UploadConfigs(
    DialogService dialogService,
    IScopedOperationRunner operationRunner
) : ComponentBase, IReloadableComponent
{
    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    [Parameter]
    public int? FocusUploadConfigId { get; set; }

    private IReadOnlyList<UploadConfigReadModel> uploadConfigs = [];

    protected override async Task OnInitializedAsync()
    {
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
                Title = L["AddUploadConfig"],
                Description = L["UploadConfigDialogDescription"],
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

    private async Task ShowEditDialogAsync(UploadConfigReadModel uploadConfigReadModel)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditUploadConfigDialog.ReleaseId)] = ReleaseId,
            [nameof(CreateOrEditUploadConfigDialog.UploadConfigId)] =
                uploadConfigReadModel.UploadConfigId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditUploadConfigDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditUploadConfig"],
                Description = L["UploadConfigDialogDescription"],
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

    private async Task DeleteConfigAsync(UploadConfigReadModel uploadConfigReadModel)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteUploadConfig"],
            L["DeleteUploadConfigConfirmation", uploadConfigReadModel.Name],
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
            (UploadConfigService service) =>
                service.DeleteAsync(uploadConfigReadModel.UploadConfigId)
        );
        await LoadUploadConfigsAsync();
    }

    private async Task LoadUploadConfigsAsync()
    {
        uploadConfigs = await operationRunner.RunAsync(
            (IUploadConfigReadRepository repository) => repository.GetUploadConfigsAsync(ReleaseId)
        );
    }

    public async Task ReloadAsync()
    {
        await LoadUploadConfigsAsync();
        StateHasChanged();
    }
}
