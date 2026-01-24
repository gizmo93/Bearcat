using Bearcat.Website.Shared;
using BearCat.Core.Domain.UseCases.ManageArchiveConfigs;
using BearCat.Core.Domain.UseCases.ManageReleases.Dto;
using BearCat.Core.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Bearcat.Website.Pages.ManageArchiveConfigs;

public partial class ArchiveConfigs(
    IDialogService dialogService) : IReloadableComponent
{
    [Parameter]
    public int ReleaseId { get; set; }

    [Parameter]
    public EventCallback<string> OnChangeAffectingOtherComponents { get; set; }

    private IReadOnlyList<ArchiveConfigDto> archiveConfigs = [];

    private IReleaseReadRepository readRepository = null!;

    protected override async Task OnInitializedAsync()
    {
        readRepository = ScopedServices.GetRequiredService<IReleaseReadRepository>();
        await LoadArchiveConfigsAsync();
    }

    private async Task ShowAddDialogAsync()
    {
        var parameters = new DialogParameters<CreateOrEditArchiveConfigDialog>
        {
            { "ReleaseId", ReleaseId }, { "FormModel", new ArchiveConfigFormModel()}
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
        await LoadArchiveConfigsAsync();
    }

    private async Task DeleteConfigAsync(ArchiveConfigDto archiveConfig)
    {
        var dialog = await dialogService.ShowMessageBoxAsync(
            title: "Delete archive config",
            message: $"Are you sure you want to delete the archive config {archiveConfig.ArchiveNameWithExtension} (Archiver: {archiveConfig.ArchiverDisplayName})?",
            yesText: "Delete",
            noText: "Cancel");

        if (dialog == true)
        {
            var archiveConfigService = ScopedServices.GetRequiredService<ArchiveConfigService>();
            await archiveConfigService.DeleteAsync(archiveConfig.ArchiveConfigId);
            await LoadArchiveConfigsAsync();
            await OnChangeAffectingOtherComponents.InvokeAsync(this.GetType().Name);
        }
    }

    private async Task LoadArchiveConfigsAsync()
    {
        archiveConfigs = await readRepository.GetArchiveConfigsAsync(ReleaseId, CancellationToken.None);
    }

    private async Task ShowEditDialogAsync(ArchiveConfigDto config)
    {
        var parameters = new DialogParameters<CreateOrEditArchiveConfigDialog>
        {
            { dlg => dlg.ReleaseId, ReleaseId },
            { dlg => dlg.FormModel, new ArchiveConfigFormModel
                {
                    ArchiveFilesBasePath = config.ArchiveFilesBasePath,
                    ArchiverName = config.ArchiverName,
                    ArchiveNamePrefix = config.ArchiveNamePrefix,
                    ArchivePassword = config.ArchivePassword,
                    ArchiveFileSizeMb = config.ArchiveFileSizeMb
                }
            },
            { dlg => dlg.ArchiveConfigId, config.ArchiveConfigId }
        };

        var dialog = await dialogService.ShowAsync<CreateOrEditArchiveConfigDialog>(
            "Edit Archive Configuration",
            parameters,
            options: new DialogOptions
            {
                BackdropClick = false,
                CloseOnEscapeKey = true,
                CloseButton = true,
                FullWidth = true,
            });

        var result = await dialog.Result;
        await LoadArchiveConfigsAsync();
    }

    public async Task ReloadAsync()
    {
        await LoadArchiveConfigsAsync();
    }
}
