using System.Globalization;
using Bearcat.Abstractions.DistributionSite;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs.Repositories;
using Bearcat.Domain.UseCases.ManagePostedLocations.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.ReadModels;
using Bearcat.Domain.UseCases.ManageRemoteSourceDownloads.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageForumPostTemplates;
using Bearcat.Website.Pages.ManagePostedLocations;
using Bearcat.Website.Pages.ManageReleases.DetailTabs;
using Bearcat.Website.Pages.ManageReleases.ProgressSteps;
using Bearcat.Website.Pages.PostToForum;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseDetail(
    NavigationManager navigationManager,
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    IJSRuntime jsRuntime
) : ComponentBase, IAsyncDisposable
{
    [Parameter]
    public int ReleaseId { get; set; }

    [SupplyParameterFromQuery(Name = "tab")]
    public string? RequestedTab { get; set; }

    [SupplyParameterFromQuery(Name = "view")]
    public string? RequestedUploadsView { get; set; }

    [SupplyParameterFromQuery(Name = "uploadConfigId")]
    public int? FocusUploadConfigId { get; set; }

    [SupplyParameterFromQuery(Name = "archiveConfigId")]
    public int? FocusArchiveConfigId { get; set; }

    [SupplyParameterFromQuery(Name = "workflow")]
    public string? Workflow { get; set; }

    private const string PostedLocationsElementId = "release-posted-locations";

    private ReleaseReadModel release = null!;
    private IReadOnlyList<string> unmanagedArchiveFolderPaths = [];
    private RemoteSourceDownloadReadModel? remoteDownloadOrigin;
    private ReleaseMetadataReadModel? releaseMetadata;
    private ReleaseInfoReadModel? releaseInfo;
    private ElementReference headerCardElement;
    private ElementReference stickyHeaderElement;
    private bool isStickyHeaderVisibilityTrackingRequested;
    private bool isScrollToPostedLocationsRequested;
    private IJSObjectReference? stickyHeaderVisibilityTrackingHandle;
    private bool isInitialized;
    private int? loadedReleaseId;
    private string? activeTab = ReleaseDetailTab.Overview;
    private ReleaseUploadsView uploadsView = ReleaseUploadsView.Configuration;
    private IReadOnlyList<ArchiveConfigReadModel> archiveConfigs = [];
    private int archiveConfigCount;
    private IReadOnlyList<ReleaseOverviewUploadReadModel> overviewUploads = [];
    private IReadOnlyList<ReleaseProgressStep> progressSteps = [];
    private int imageUploadConfigCount;
    private PostedLocations? postedLocations;
    private bool hasActiveForumRegistration;
    private readonly Dictionary<string, IReloadableComponent> reloadableComponents = new();

    private bool IsPostQueueWorkflow =>
        string.Equals(Workflow, "postqueue", StringComparison.OrdinalIgnoreCase);

    private string? CoverUrl => releaseMetadata?.CoverUrl;

    private bool IsPostingToForumPending =>
        release.OnlineUploadConfigsCount > 0
        && progressSteps.Single(step => step.Kind == ReleaseProgressStepKind.Posted).State
            == ReleaseProgressStepState.Pending;

    private bool HasOfflineUploadConfigs =>
        release.OnlineUploadConfigsCount < release.ActiveUploadConfigsCount;

    private string UploadConfigCountText =>
        HasOfflineUploadConfigs
            ? string.Create(
                CultureInfo.CurrentCulture,
                $"{release.OnlineUploadConfigsCount}/{release.ActiveUploadConfigsCount}"
            )
            : release.ActiveUploadConfigsCount.ToString(CultureInfo.CurrentCulture);

    private string ArchiveConfigCountText =>
        archiveConfigCount.ToString(CultureInfo.CurrentCulture);

    private string ImageUploadConfigCountText =>
        imageUploadConfigCount.ToString(CultureInfo.CurrentCulture);

    private IReadOnlyList<SelectOption<string>> TabSelectOptions =>
        [
            new(ReleaseDetailTab.Overview, L["ReleaseOverview"]),
            new(ReleaseDetailTab.ReleaseInfos, L["ReleaseInfo"]),
            new(ReleaseDetailTab.Archives, $"{L["Archives"]} ({ArchiveConfigCountText})"),
            new(ReleaseDetailTab.Uploads, $"{L["Uploads"]} ({UploadConfigCountText})"),
            new(ReleaseDetailTab.Images, $"{L["Images"]} ({ImageUploadConfigCountText})"),
        ];

    protected override async Task OnParametersSetAsync()
    {
        var tabSelection = ReleaseDetailTabSelectionResolver.Resolve(
            RequestedTab,
            RequestedUploadsView,
            FocusUploadConfigId
        );
        activeTab = tabSelection.Tab;
        uploadsView = tabSelection.UploadsView;

        if (loadedReleaseId != ReleaseId)
        {
            await LoadReleaseAsync();
        }
    }

    private async Task LoadReleaseAsync()
    {
        var releaseReadModel = await operationRunner.RunAsync(
            (IReleaseReadRepository repository) => repository.GetReleaseAsync(ReleaseId)
        );

        if (releaseReadModel is null)
        {
            navigationManager.NotFound();
            return;
        }

        release = releaseReadModel;
        await LoadUnmanagedArchiveFolderPathsAsync();
        await LoadReleaseMetadataAndReleaseInfoAsync();
        await LoadArchiveConfigsAsync();
        await LoadImageUploadConfigCountAsync();
        await LoadOverviewUploadsAndProgressStepsAsync();
        await LoadHasActiveForumRegistrationAsync();
        remoteDownloadOrigin = await operationRunner.RunAsync(
            (IRemoteSourceDownloadReadRepository repository) =>
                repository.GetByReleaseIdAsync(ReleaseId)
        );
        loadedReleaseId = ReleaseId;
        isInitialized = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!isInitialized)
        {
            return;
        }

        await AttachStickyHeaderVisibilityTrackingAsync();
        await ScrollToPostedLocationsIfRequestedAsync();
    }

    private async Task AttachStickyHeaderVisibilityTrackingAsync()
    {
        if (isStickyHeaderVisibilityTrackingRequested)
        {
            return;
        }

        isStickyHeaderVisibilityTrackingRequested = true;

        try
        {
            stickyHeaderVisibilityTrackingHandle = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "bearcat.releaseStickyHeader.attach",
                headerCardElement,
                stickyHeaderElement
            );
        }
        catch (JSDisconnectedException) { }
    }

    private async Task ScrollToPostedLocationsIfRequestedAsync()
    {
        if (!isScrollToPostedLocationsRequested)
        {
            return;
        }

        isScrollToPostedLocationsRequested = false;

        try
        {
            await jsRuntime.InvokeVoidAsync(
                "bearcat.scrollElementIntoViewById",
                PostedLocationsElementId
            );
        }
        catch (JSDisconnectedException) { }
    }

    private async Task LoadReleaseMetadataAndReleaseInfoAsync()
    {
        await operationRunner.RunAsync<IReleaseReadRepository>(async repository =>
        {
            releaseMetadata = await repository.GetReleaseMetadataAsync(ReleaseId);
            releaseInfo = await repository.GetReleaseInfoAsync(ReleaseId);
        });
    }

    private async Task LoadArchiveConfigsAsync()
    {
        archiveConfigs = await operationRunner.RunAsync(
            (IReleaseReadRepository repository) =>
                repository.GetArchiveConfigsAsync(ReleaseId, CancellationToken.None)
        );
        archiveConfigCount = archiveConfigs.Count;
    }

    private async Task LoadImageUploadConfigCountAsync()
    {
        var imageUploadConfigs = await operationRunner.RunAsync(
            (IImageUploadConfigReadRepository repository) =>
                repository.GetImageUploadConfigsAsync(ReleaseId)
        );
        imageUploadConfigCount = imageUploadConfigs.Count;
    }

    private async Task LoadHasActiveForumRegistrationAsync()
    {
        var distributionSiteRegistrations = await operationRunner.RunAsync(
            (IDistributionSiteRegistrationReadRepository repository) => repository.GetAllAsync()
        );
        hasActiveForumRegistration = distributionSiteRegistrations.Any(registration =>
            registration.Kind == DistributionSiteKind.Forum && registration.IsActive
        );
    }

    private async Task LoadOverviewUploadsAndProgressStepsAsync()
    {
        overviewUploads = await operationRunner.RunAsync(
            (IReleaseReadRepository repository) => repository.GetReleaseOverviewAsync(ReleaseId)
        );
        var postedLocationReadModels = await operationRunner.RunAsync(
            (IPostedLocationReadRepository repository) => repository.GetForReleaseAsync(ReleaseId)
        );
        progressSteps = ReleaseProgressStepCalculator.Calculate(
            release,
            releaseInfo,
            releaseMetadata,
            archiveConfigs,
            overviewUploads,
            postedLocationReadModels.Count
        );
    }

    private async Task ReloadProgressAsync()
    {
        var releaseReadModel = await operationRunner.RunAsync(
            (IReleaseReadRepository repository) => repository.GetReleaseAsync(ReleaseId)
        );

        if (releaseReadModel is null)
        {
            navigationManager.NotFound();
            return;
        }

        release = releaseReadModel;
        await LoadReleaseMetadataAndReleaseInfoAsync();
        await LoadArchiveConfigsAsync();
        await LoadOverviewUploadsAndProgressStepsAsync();
    }

    private async Task LoadUnmanagedArchiveFolderPathsAsync()
    {
        unmanagedArchiveFolderPaths =
            release.ReleaseType is ReleaseType.Unmanaged
                ? await operationRunner.RunAsync(
                    (IReleaseReadRepository repository) =>
                        repository.GetUnmanagedArchiveFolderPathsAsync(release.ReleaseId)
                )
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

        await ReloadProgressAsync();
    }

    private async Task HandleOverviewRefreshRequestedAsync()
    {
        await ReloadPostedLocationsAsync();
        await ReloadProgressAsync();
    }

    private async Task ReloadPostedLocationsAsync()
    {
        if (postedLocations is not null)
        {
            await postedLocations.ReloadAsync();
        }
    }

    private async Task ShowPostToForumDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(PostToForumDialog.EntityId)] = release.ReleaseId,
            [nameof(PostToForumDialog.EntityName)] = release.Name,
            [nameof(PostToForumDialog.TemplateType)] = ForumPostTemplateType.Release,
        };

        await dialogService.OpenAsync<PostToForumDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["PostNamedReleaseToForum", release.Name],
                Description = L["PostToForumDescription"],
                Size = DialogSize.ExtraLarge,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (reloadableComponents.TryGetValue(nameof(ReleaseOverview), out var releaseOverview))
        {
            await releaseOverview.ReloadAsync();
        }

        await ReloadPostedLocationsAsync();
        await ReloadProgressAsync();
    }

    private async Task RenderForumPostAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(RenderForumPostDialog.EntityId)] = release.ReleaseId,
            [nameof(RenderForumPostDialog.Type)] = ForumPostTemplateType.Release,
        };

        await dialogService.OpenAsync<RenderForumPostDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["RenderForumPostForRelease", release.Name],
                Description = L["RenderForumPostDescription"],
                Size = DialogSize.Full,
                ShowClose = true,
            }
        );
    }

    private void HandleTabChanged(string? value)
    {
        activeTab = value;
    }

    private void HandleProgressStepSelected(ReleaseProgressStepKind kind)
    {
        activeTab = ReleaseProgressStepTabResolver.GetTab(kind);

        if (kind is ReleaseProgressStepKind.Uploaded)
        {
            uploadsView = ReleaseUploadsView.History;
        }

        if (kind is ReleaseProgressStepKind.Posted)
        {
            isScrollToPostedLocationsRequested = true;
        }
    }

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
                PrimaryLanguageCode = release.PrimaryLanguageCode ?? string.Empty,
                ReleaseGroupId = release.ReleaseGroupId,
                ExcludeFromAutoCleanup = release.ExcludeFromAutoCleanup,
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

        await operationRunner.RunAsync(
            (ReleaseService service) => service.DeleteAsync(release.ReleaseId)
        );
        navigationManager.NavigateTo("/releases");
    }

    private bool CanConvertToUnmanaged => release.ReleaseType is ReleaseType.Managed;

    private bool IsUnmanaged => release.ReleaseType is ReleaseType.Unmanaged;

    private async Task DeleteLocalArchivesAsync()
    {
        var preview = await operationRunner.RunAsync(
            (ReleaseService service) => service.GetArchiveDeletionPreviewAsync(release.ReleaseId)
        );

        if (!preview.CanDelete)
        {
            toastService.Error(L["DeleteLocalArchivesNotReady"]);
            return;
        }

        var confirmation = IsUnmanaged
            ? L[
                "DeleteLocalArchivesConfirmationMirror",
                string.Join(", ", preview.MirrorHosterNames),
                string.Join(Environment.NewLine, preview.DeletableArchiveFolderPaths)
            ]
            : L[
                "DeleteLocalArchivesConfirmationManaged",
                string.Join(Environment.NewLine, preview.DeletableArchiveFolderPaths)
            ];

        var result = await dialogService.ConfirmAsync(
            L["DeleteLocalArchivesTitle", release.Name],
            confirmation,
            new ConfirmDialogOptions
            {
                ConfirmText = L["DeleteLocalArchives"],
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
            await operationRunner.RunAsync(
                (ReleaseService service) => service.DeleteLocalArchivesAsync(release.ReleaseId)
            );
            toastService.Success(L["LocalArchivesDeleted", release.Name]);
            await ReloadReleaseAsync();
        }
        catch (InvalidOperationException exception)
        {
            toastService.Error(exception.Message);
        }
    }

    private async Task ConvertToUnmanagedAsync()
    {
        var preview = await operationRunner.RunAsync(
            (ReleaseService service) =>
                service.GetUnmanagedConversionPreviewAsync(release.ReleaseId)
        );

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
            await operationRunner.RunAsync(
                (ReleaseService service) => service.ConvertToUnmanagedAsync(release.ReleaseId)
            );
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

        try
        {
            await operationRunner.RunAsync(
                (ReleaseService service) =>
                    service.ConvertToManagedAsync(release.ReleaseId, folderPath)
            );
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

        var releaseTemplateId = await operationRunner.RunAsync(
            (ReleaseTemplateService service) =>
                service.CreateTemplateFromReleaseAsync(release.ReleaseId, result.Value)
        );

        navigationManager.NavigateTo($"/release-templates/{releaseTemplateId}");
    }

    private async Task ReloadReleaseAsync()
    {
        await ReloadProgressAsync();
        await LoadUnmanagedArchiveFolderPathsAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (stickyHeaderVisibilityTrackingHandle is null)
        {
            return;
        }

        try
        {
            await stickyHeaderVisibilityTrackingHandle.InvokeVoidAsync("detach");
            await stickyHeaderVisibilityTrackingHandle.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
    }
}
