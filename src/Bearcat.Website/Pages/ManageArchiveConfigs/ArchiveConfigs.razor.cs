using Bearcat.Domain.UseCases.ManageArchiveConfigs;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.ManageArchiveConfigs;

public partial class ArchiveConfigs(
    DialogService dialogService,
    ToastService toastService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    IServiceScopeFactory serviceScopeFactory
) : OwningComponentBase, IReloadableComponent
{
    [Parameter]
    public int ReleaseId { get; set; }

    [Parameter]
    public ReleaseType ReleaseType { get; set; } = ReleaseType.Managed;

    [Parameter]
    public int? FocusArchiveConfigId { get; set; }

    [Parameter]
    public EventCallback<string> OnChangeAffectingOtherComponents { get; set; }

    private IReadOnlyList<ArchiveConfigReadModel> archiveConfigs = [];
    private string ArchiveGridClass =>
        ReleaseType is ReleaseType.Unmanaged
            ? "lg:grid-cols-[minmax(0,1.35fr)_120px_110px_84px]"
            : "lg:grid-cols-[minmax(0,1.35fr)_120px_110px_120px_minmax(0,1fr)_84px]";

    protected override async Task OnInitializedAsync()
    {
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

    private async Task DeleteConfigAsync(ArchiveConfigReadModel archiveConfig)
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

        await using (var scope = serviceScopeFactory.CreateAsyncScope())
        {
            var archiveConfigService =
                scope.ServiceProvider.GetRequiredService<ArchiveConfigService>();
            await archiveConfigService.DeleteAsync(archiveConfig.ArchiveConfigId);
        }

        await LoadArchiveConfigsAsync();
        await OnChangeAffectingOtherComponents.InvokeAsync(GetType().Name);
    }

    private async Task ChangeArchiveFolderAsync(ArchiveConfigReadModel archiveConfig)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] =
                workingDirectoriesConfig.Value.GetWorkingDirectories(),
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = archiveConfig.ArchiveFilesBasePath,
        };

        var folderResult = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["ChangeArchiveFolderDialogTitle"],
                Description = L["ChangeArchiveFolderDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );

        if (folderResult.Cancelled)
        {
            return;
        }

        var folderPath = folderResult.GetData<string>();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        await ApplyArchiveFolderAsync(
            archiveConfig.ArchiveConfigId,
            folderPath,
            confirmContentChange: false
        );
    }

    private async Task ApplyArchiveFolderAsync(
        int archiveConfigId,
        string folderPath,
        bool confirmContentChange
    )
    {
        try
        {
            ArchiveFolderChangeResult result;
            await using (var scope = serviceScopeFactory.CreateAsyncScope())
            {
                var archiveConfigService =
                    scope.ServiceProvider.GetRequiredService<ArchiveConfigService>();
                result = await archiveConfigService.SetArchiveFolderAsync(
                    archiveConfigId: archiveConfigId,
                    archiveFolderPath: folderPath,
                    confirmContentChange: confirmContentChange,
                    cancellationToken: CancellationToken.None
                );
            }

            if (result is ArchiveFolderChangeResult.ConfirmationRequired)
            {
                await ConfirmReimportAsync(archiveConfigId, folderPath);
                return;
            }

            toastService.Success(
                result is ArchiveFolderChangeResult.Relocated
                    ? L["ArchiveFolderRelocated"]
                    : L["ArchiveFolderReimported"]
            );
            await LoadArchiveConfigsAsync();
            await OnChangeAffectingOtherComponents.InvokeAsync(GetType().Name);
        }
        catch (InvalidOperationException ex)
        {
            toastService.Error(ex.Message);
        }
    }

    private async Task ConfirmReimportAsync(int archiveConfigId, string folderPath)
    {
        var result = await dialogService.ConfirmAsync(
            L["ReimportArchiveFolder"],
            L["ReimportArchiveFolderConfirmation"],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Continue"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        await ApplyArchiveFolderAsync(archiveConfigId, folderPath, confirmContentChange: true);
    }

    private async Task LoadArchiveConfigsAsync()
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var readRepository = scope.ServiceProvider.GetRequiredService<IReleaseReadRepository>();
        archiveConfigs = await readRepository.GetArchiveConfigsAsync(
            ReleaseId,
            CancellationToken.None
        );
    }

    private async Task ShowEditDialogAsync(ArchiveConfigReadModel config)
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
