using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManagePostedLocations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseDetail(
    NavigationManager navigationManager,
    DialogService dialogService,
    ToastService toastService,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig
) : OwningComponentBase
{
    [Parameter]
    public int ReleaseId { get; set; }

    [SupplyParameterFromQuery(Name = "tab")]
    public string? RequestedTab { get; set; }

    [SupplyParameterFromQuery(Name = "uploadConfigId")]
    public int? FocusUploadConfigId { get; set; }

    [SupplyParameterFromQuery(Name = "archiveConfigId")]
    public int? FocusArchiveConfigId { get; set; }

    [SupplyParameterFromQuery(Name = "workflow")]
    public string? Workflow { get; set; }

    private IReleaseReadRepository releaseReadRepository = null!;
    private ReleaseReadModel release = null!;
    private IReadOnlyList<string> unmanagedArchiveFolderPaths = [];
    private bool isInitialized;
    private int? loadedReleaseId;
    private string? activeTab = "overview";
    private PostedLocations? postedLocations;
    private readonly Dictionary<string, IReloadableComponent> reloadableComponents = new();

    private bool IsPostQueueWorkflow =>
        string.Equals(Workflow, "postqueue", StringComparison.OrdinalIgnoreCase);

    protected override void OnInitialized()
    {
        releaseReadRepository = ScopedServices.GetRequiredService<IReleaseReadRepository>();
    }

    protected override async Task OnParametersSetAsync()
    {
        activeTab = NormalizeTab(RequestedTab);

        if (loadedReleaseId != ReleaseId)
        {
            await LoadReleaseAsync();
        }
    }

    private async Task LoadReleaseAsync()
    {
        var releaseReadModel = await releaseReadRepository.GetReleaseAsync(ReleaseId);

        if (releaseReadModel is null)
        {
            navigationManager.NotFound();
            return;
        }

        release = releaseReadModel;
        await LoadUnmanagedArchiveFolderPathsAsync();
        loadedReleaseId = ReleaseId;
        isInitialized = true;
    }

    private async Task LoadUnmanagedArchiveFolderPathsAsync()
    {
        unmanagedArchiveFolderPaths =
            release.ReleaseType is ReleaseType.Unmanaged
                ? await releaseReadRepository.GetUnmanagedArchiveFolderPathsAsync(release.ReleaseId)
                : [];
    }

    private async Task HandleChangeAffectingOtherComponentsAsync(string componentName)
    {
        var affectedComponents = reloadableComponents
            .Where(c => c.Key != componentName)
            .Select(c => c.Value);

        foreach (var component in affectedComponents)
        {
            await component.ReloadAsync();
        }
    }

    private async Task ReloadPostedLocationsAsync()
    {
        if (postedLocations is not null)
        {
            await postedLocations.ReloadAsync();
        }
    }

    private Task HandleTabChangedAsync(string? value)
    {
        activeTab = NormalizeTab(value);
        return Task.CompletedTask;
    }

    private static string NormalizeTab(string? tab) =>
        tab switch
        {
            "overview" => "overview",
            "release-infos" => "release-infos",
            "upload-configs" => "upload-configs",
            "image-upload-configs" => "image-upload-configs",
            "uploads" => "uploads",
            "image-uploads" => "image-uploads",
            "archives" => "archives",
            _ => "overview",
        };

    private async Task ShowEditReleaseDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditReleaseDialog.ReleaseId)] = release.ReleaseId,
            [nameof(CreateOrEditReleaseDialog.FormModel)] = new ReleaseFormModel
            {
                Name = release.Name,
                FolderPath = release.ReleaseFolderPath ?? string.Empty,
                ReleaseType = release.ReleaseType,
                ReleaseContentType = release.ReleaseContentType,
                ReleaseGroupId = release.ReleaseGroupId,
                IsEdit = true,
            },
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditReleaseDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", release.Name],
                Description = L["EditReleaseDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await ReloadReleaseAsync();
        }
    }

    private async Task DeleteReleaseAsync()
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteReleaseTitle", release.Name],
            L["DeleteReleaseConfirmation", release.Name],
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

        var service = ScopedServices.GetRequiredService<ReleaseService>();
        await service.DeleteAsync(release.ReleaseId);
        navigationManager.NavigateTo("/releases");
    }

    private bool CanConvertToUnmanaged => release.ReleaseType is ReleaseType.Managed;

    private bool IsUnmanaged => release.ReleaseType is ReleaseType.Unmanaged;

    private async Task ConvertToUnmanagedAsync()
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ReleaseService>();
        var preview = await service.GetUnmanagedConversionPreviewAsync(release.ReleaseId);

        if (!preview.CanConvert)
        {
            toastService.Error(L["ConvertToUnmanagedNotReady"]);
            return;
        }

        var folderPath = preview.ReleaseFolderPath ?? string.Empty;
        var confirmation = preview.ArchivesInsideReleaseFolder
            ? L["ConvertToUnmanagedConfirmationArchivesInside", folderPath]
            : L["ConvertToUnmanagedConfirmation", folderPath];

        var result = await dialogService.ConfirmAsync(
            L["ConvertToUnmanagedTitle", release.Name],
            confirmation,
            new ConfirmDialogOptions
            {
                ConfirmText = L["ConvertToUnmanaged"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        try
        {
            await service.ConvertToUnmanagedAsync(release.ReleaseId);
            toastService.Success(L["ReleaseConvertedToUnmanaged", release.Name]);
            await ReloadReleaseAsync();
        }
        catch (InvalidOperationException exception)
        {
            toastService.Error(exception.Message);
        }
    }

    private async Task ConvertToManagedAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] =
                workingDirectoriesConfig.Value.GetWorkingDirectories(),
        };

        var folderResult = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SelectReleaseFolder"],
                Description = L["SelectReleaseFolderDescription"],
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

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ReleaseService>();

        try
        {
            await service.ConvertToManagedAsync(release.ReleaseId, folderPath);
            toastService.Success(L["ReleaseConvertedToManaged", release.Name]);
            await ReloadReleaseAsync();
        }
        catch (InvalidOperationException exception)
        {
            toastService.Error(exception.Message);
        }
    }

    private async Task SaveAsTemplateAsync()
    {
        var result = await dialogService.PromptAsync(
            L["SaveAsTemplate"],
            L["SaveAsTemplateDescription"],
            new PromptDialogOptions
            {
                ConfirmText = L["Save"],
                CancelText = L["Cancel"],
                DefaultValue = release.Name,
                Placeholder = L["ReleaseTemplateNamePlaceholder"],
                Required = true,
                MaxLength = 200,
            }
        );

        if (result.Cancelled || string.IsNullOrWhiteSpace(result.Value))
        {
            return;
        }

        var service = ScopedServices.GetRequiredService<ReleaseTemplateService>();
        var releaseTemplateId = await service.CreateTemplateFromReleaseAsync(
            release.ReleaseId,
            result.Value
        );

        navigationManager.NavigateTo($"/release-templates/{releaseTemplateId}");
    }

    private async Task ReloadReleaseAsync()
    {
        var releaseReadModel = await releaseReadRepository.GetReleaseAsync(ReleaseId);

        if (releaseReadModel is null)
        {
            navigationManager.NotFound();
            return;
        }

        release = releaseReadModel;
        await LoadUnmanagedArchiveFolderPathsAsync();
    }
}
