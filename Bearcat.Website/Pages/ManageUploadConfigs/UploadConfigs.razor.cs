using BearCat.Core.Domain.UseCases.ManageUploadConfigs;
using BearCat.Core.Domain.UseCases.ManageUploadConfigs.Dto;
using BearCat.Core.Domain.UseCases.ManageUploadConfigs.Repositories;
using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Bearcat.Website.Pages.ManageUploadConfigs;

public partial class UploadConfigs(IDialogService dialogService) : IReloadableComponent
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
        var parameters = new DialogParameters<CreateOrEditUploadConfigDialog> { { dlg => dlg.ReleaseId, ReleaseId } };

        var dialog = await dialogService.ShowAsync<CreateOrEditUploadConfigDialog>("Add Upload Config", parameters,
            new DialogOptions
            {
                BackdropClick = false, CloseOnEscapeKey = false, CloseButton = true, FullWidth = true,
            });

        await dialog.Result;
        await LoadUploadConfigsAsync();
    }

    private async Task ShowEditDialogAsync(UploadConfigDto uploadConfigDto)
    {
        var parameters = new DialogParameters<CreateOrEditUploadConfigDialog>
        {
            { dlg => dlg.ReleaseId, ReleaseId }, { dlg => dlg.UploadConfigId, uploadConfigDto.UploadConfigId }
        };

        var dialog = await dialogService.ShowAsync<CreateOrEditUploadConfigDialog>(
            "Edit Upload Config",
            parameters,
            new DialogOptions
            {
                BackdropClick = false, CloseOnEscapeKey = false, CloseButton = true, FullWidth = true,
            });

        await dialog.Result;
        await LoadUploadConfigsAsync();
    }

    private async Task DeleteConfigAsync(UploadConfigDto uploadConfigDto)
    {
        var dialog = await dialogService.ShowMessageBoxAsync(
            title: "Delete Upload Config",
            message: $"Are you sure you want to delete the upload config {uploadConfigDto.Name}?",
            yesText: "Delete",
            noText: "Cancel");

        if (dialog == true)
        {
            var service = ScopedServices.GetRequiredService<UploadConfigService>();
            await service.DeleteAsync(uploadConfigDto.UploadConfigId);
            await LoadUploadConfigsAsync();
        }
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
