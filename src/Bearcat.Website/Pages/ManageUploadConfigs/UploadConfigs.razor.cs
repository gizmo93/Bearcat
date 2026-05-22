using Bearcat.Domain.UseCases.ManageUploadConfigs;
using Bearcat.Domain.UseCases.ManageUploadConfigs.ReadModels;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageUploadConfigs;

public partial class UploadConfigs(DialogService dialogService)
    : OwningComponentBase,
        IReloadableComponent
{
    [Parameter]
    [EditorRequired]
    public int ReleaseId { get; set; }

    [Parameter]
    public int? FocusUploadConfigId { get; set; }

    private IReadOnlyList<UploadConfigReadModel> uploadConfigs = [];
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

        var service = ScopedServices.GetRequiredService<UploadConfigService>();
        await service.DeleteAsync(uploadConfigReadModel.UploadConfigId);
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
