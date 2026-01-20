using BearCat.Core.Domain.UseCases.ManageReleases.Dto;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Bearcat.Frontend.Components.Pages.ManageReleases;

public partial class ArchiveConfigs(
    IReleaseReadRepository readRepository,
    IDialogService dialogService)
    : ComponentBase
{
    [Parameter] public int ReleaseId { get; set; }

    private IReadOnlyList<ArchiveConfigDto> archiveConfigs = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadArchiveConfigsAsync();
    }

    private async Task ShowAddDialogAsync()
    {
        var parameters = new DialogParameters<CreateOrEditArchiveConfigDialog>
        {
            { "ReleaseId", ReleaseId }, { "FormModel", new ArchiveConfigFormModel { IsEdit = false } }
        };

        var dialog = await dialogService.ShowAsync<CreateOrEditArchiveConfigDialog>(
            "Add Archive Configuration",
            parameters,
            options: new DialogOptions
            {
                BackdropClick = false,
                CloseOnEscapeKey = true,
                CloseButton = true,
                FullWidth = true,
            });

        var result = await dialog.Result;
    }

    private async Task LoadArchiveConfigsAsync()
    {
        archiveConfigs = await readRepository.GetArchiveConfigsAsync(ReleaseId, CancellationToken.None);
    }
}
