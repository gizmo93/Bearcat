using Bearcat.Domain.UseCases.ManageArchiveConfigs;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageArchiveConfigs;

public partial class ArchiveConfigs(DialogService dialogService)
    : OwningComponentBase,
        IReloadableComponent
{
    [Parameter]
    public int ReleaseId { get; set; }

    [Parameter]
    public int? FocusArchiveConfigId { get; set; }

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
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditArchiveConfigDialog.ReleaseId)] = ReleaseId,
            [nameof(CreateOrEditArchiveConfigDialog.FormModel)] = new ArchiveConfigFormModel(),
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditArchiveConfigDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["AddArchiveConfiguration"],
                Description = L["ArchiveConfigurationDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadArchiveConfigsAsync();
        }
    }

    private async Task DeleteConfigAsync(ArchiveConfigDto archiveConfig)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteArchiveConfig"],
            L[
                "DeleteArchiveConfigConfirmation",
                archiveConfig.ArchiveNameWithExtension ?? archiveConfig.Name,
                archiveConfig.ArchiverDisplayName
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

        var archiveConfigService = ScopedServices.GetRequiredService<ArchiveConfigService>();
        await archiveConfigService.DeleteAsync(archiveConfig.ArchiveConfigId);
        await LoadArchiveConfigsAsync();
        await OnChangeAffectingOtherComponents.InvokeAsync(GetType().Name);
    }

    private async Task LoadArchiveConfigsAsync()
    {
        archiveConfigs = await readRepository.GetArchiveConfigsAsync(
            ReleaseId,
            CancellationToken.None
        );
    }

    private async Task ShowEditDialogAsync(ArchiveConfigDto config)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditArchiveConfigDialog.ReleaseId)] = ReleaseId,
            [nameof(CreateOrEditArchiveConfigDialog.FormModel)] = new ArchiveConfigFormModel
            {
                ArchiveFilesBasePath = config.ArchiveFilesBasePath,
                ArchiverName = config.ArchiverName,
                ArchiveNamePrefix = config.ArchiveNamePrefix,
                ArchivePassword = config.ArchivePassword,
                ArchiveFileSizeMb = config.ArchiveFileSizeMb,
                Name = config.Name,
            },
            [nameof(CreateOrEditArchiveConfigDialog.ArchiveConfigId)] = config.ArchiveConfigId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditArchiveConfigDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditArchiveConfiguration"],
                Description = L["ArchiveConfigurationDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadArchiveConfigsAsync();
        }
    }

    public async Task ReloadAsync()
    {
        await LoadArchiveConfigsAsync();
    }
}
