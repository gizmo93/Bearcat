using System.Globalization;
using Bearcat.Abstractions.DistributionSite;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Domain.UseCases.ManageLinkCrypterContainers;
using Bearcat.Domain.UseCases.ManagePostedLocations.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Pages.ManageForumPostTemplates;
using Bearcat.Website.Pages.ManagePostedLocations;
using Bearcat.Website.Pages.ManageReleaseCollections.DetailTabs;
using Bearcat.Website.Pages.ManageReleaseCollections.Overview;
using Bearcat.Website.Pages.ManageReleaseCollections.ProgressSteps;
using Bearcat.Website.Pages.PostToForum;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared.ProgressSteps;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class ReleaseCollectionDetail(
    DialogService dialogService,
    ToastService toastService,
    NavigationManager navigationManager,
    IScopedOperationRunner operationRunner,
    IJSRuntime jsRuntime
) : ComponentBase, IAsyncDisposable
{
    [Parameter]
    public int ReleaseCollectionId { get; set; }

    [SupplyParameterFromQuery(Name = "tab")]
    public string? RequestedTab { get; set; }

    [SupplyParameterFromQuery(Name = "workflow")]
    public string? Workflow { get; set; }

    private const string PostedLocationsElementId = "release-collection-posted-locations";
    private const string LinkContainersElementId = "release-collection-link-containers";
    private const int CollapsedDescriptionMaximumLength = 160;

    private ReleaseCollectionDetailReadModel releaseCollection = null!;
    private IReadOnlyList<CollectionImageUploadReadModel> imageUploads = [];
    private IReadOnlyList<ReleaseCollectionProgressStep> progressSteps = [];
    private ElementReference headerCardElement;
    private ElementReference stickyHeaderElement;
    private bool isStickyHeaderVisibilityTrackingRequested;
    private string? elementIdToScrollIntoView;
    private IJSObjectReference? stickyHeaderVisibilityTrackingHandle;
    private PostedLocations? postedLocations;
    private bool hasActiveForumRegistration;
    private bool isInitialized;
    private bool isResolvingMetadata;
    private bool isDescriptionExpanded;
    private int? loadedReleaseCollectionId;
    private string? activeTab = ReleaseCollectionDetailTab.Overview;

    private bool IsPostQueueWorkflow =>
        string.Equals(Workflow, "postqueue", StringComparison.OrdinalIgnoreCase);

    private string? CoverUrl => releaseCollection.Metadata?.CoverUrl;

    private bool IsLinkContainersSectionVisible =>
        releaseCollection.UploadSlots.Any(
            CollectionLinkContainerOverviewService.HasSharedLinkCryptersOrContainers
        );

    private string? Description => releaseCollection.Metadata?.Description;

    private bool IsDescriptionToggleVisible =>
        Description is not null && Description.Length > CollapsedDescriptionMaximumLength;

    private bool IsPostingToForumPending =>
        releaseCollection.Releases.Any(release => release.OnlineUploadConfigsCount > 0)
        && progressSteps.Single(step => step.Kind == ReleaseCollectionProgressStepKind.Posted).State
            == ProgressStepState.Pending;

    private string ReleaseCountText =>
        releaseCollection.Releases.Count == 1
            ? L["ReleaseCollectionReleaseCountOne"]
            : L["ReleaseCollectionReleaseCount", releaseCollection.Releases.Count];

    private string UploadSlotCountText =>
        releaseCollection.UploadSlots.Count.ToString(CultureInfo.CurrentCulture);

    private string ImageUploadConfigCountText =>
        imageUploads.Count.ToString(CultureInfo.CurrentCulture);

    private IReadOnlyList<SelectOption<string>> TabSelectOptions =>
        [
            new(ReleaseCollectionDetailTab.Overview, L["Overview"]),
            new(
                ReleaseCollectionDetailTab.UploadSlots,
                $"{L["CollectionUploadSlots"]} ({UploadSlotCountText})"
            ),
            new(ReleaseCollectionDetailTab.Images, $"{L["Images"]} ({ImageUploadConfigCountText})"),
        ];

    protected override async Task OnParametersSetAsync()
    {
        activeTab = GetTabFromRequestedTab(RequestedTab);

        if (loadedReleaseCollectionId != ReleaseCollectionId)
        {
            isDescriptionExpanded = false;
            await LoadReleaseCollectionAsync();
            await LoadHasActiveForumRegistrationAsync();
        }
    }

    private static string GetTabFromRequestedTab(string? requestedTab) =>
        requestedTab switch
        {
            ReleaseCollectionDetailTab.UploadSlots => ReleaseCollectionDetailTab.UploadSlots,
            ReleaseCollectionDetailTab.Images => ReleaseCollectionDetailTab.Images,
            _ => ReleaseCollectionDetailTab.Overview,
        };

    private async Task LoadReleaseCollectionAsync()
    {
        var detail = await operationRunner.RunAsync(
            (IReleaseCollectionReadRepository repository) =>
                repository.GetDetailAsync(ReleaseCollectionId)
        );

        if (detail is null)
        {
            navigationManager.NotFound();
            return;
        }

        releaseCollection = detail;
        await LoadImageUploadsAsync();
        await LoadProgressStepsAsync();
        loadedReleaseCollectionId = ReleaseCollectionId;
        isInitialized = true;
    }

    private async Task LoadImageUploadsAsync()
    {
        imageUploads = await operationRunner.RunAsync(
            (IReleaseCollectionReadRepository repository) =>
                repository.GetImageUploadsAsync(ReleaseCollectionId)
        );
    }

    private async Task LoadProgressStepsAsync()
    {
        var postedLocationReadModels = await operationRunner.RunAsync(
            (IPostedLocationReadRepository repository) =>
                repository.GetForCollectionAsync(ReleaseCollectionId)
        );
        progressSteps = ReleaseCollectionProgressStepService.BuildReleaseCollectionProgressSteps(
            releaseCollection,
            imageUploads,
            postedLocationReadModels.Count
        );
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!isInitialized)
        {
            return;
        }

        await AttachStickyHeaderVisibilityTrackingAsync();
        await ScrollRequestedElementIntoViewAsync();
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

    private async Task ScrollRequestedElementIntoViewAsync()
    {
        if (elementIdToScrollIntoView is null)
        {
            return;
        }

        var elementId = elementIdToScrollIntoView;
        elementIdToScrollIntoView = null;

        try
        {
            await jsRuntime.InvokeVoidAsync("bearcat.scrollElementIntoViewById", elementId);
        }
        catch (JSDisconnectedException) { }
    }

    private void HandleTabChanged(string? value)
    {
        activeTab = value;
    }

    private void HandleProgressStepSelected(ReleaseCollectionProgressStepKind kind)
    {
        switch (kind)
        {
            case ReleaseCollectionProgressStepKind.Info:
            case ReleaseCollectionProgressStepKind.Releases:
                activeTab = ReleaseCollectionDetailTab.Overview;
                break;
            case ReleaseCollectionProgressStepKind.LinkContainers
                when IsLinkContainersSectionVisible:
                activeTab = ReleaseCollectionDetailTab.Overview;
                elementIdToScrollIntoView = LinkContainersElementId;
                break;
            case ReleaseCollectionProgressStepKind.LinkContainers:
                activeTab = ReleaseCollectionDetailTab.UploadSlots;
                break;
            case ReleaseCollectionProgressStepKind.Images:
                activeTab = ReleaseCollectionDetailTab.Images;
                break;
            case ReleaseCollectionProgressStepKind.Posted:
                activeTab = ReleaseCollectionDetailTab.Overview;
                elementIdToScrollIntoView = PostedLocationsElementId;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private void ToggleDescription()
    {
        isDescriptionExpanded = !isDescriptionExpanded;
    }

    private async Task HandleImageUploadsChangedAsync()
    {
        await LoadImageUploadsAsync();
        await LoadProgressStepsAsync();
    }

    private async Task ShowEditSettingsDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(EditReleaseCollectionSettingsDialog.ReleaseCollectionId)] = ReleaseCollectionId,
            [nameof(EditReleaseCollectionSettingsDialog.ContentType)] =
                releaseCollection.ReleaseContentType,
            [nameof(EditReleaseCollectionSettingsDialog.PrimaryLanguageCode)] =
                releaseCollection.PrimaryLanguageCode,
        };

        var dialog = await dialogService.OpenAsync<EditReleaseCollectionSettingsDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditCollectionSettings"],
                Description = L["EditCollectionSettingsDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        toastService.Success(L["ReleaseCollectionSettingsUpdated"]);
        await LoadReleaseCollectionAsync();
    }

    private async Task ShowEditMetadataDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(EditCollectionMetadataDialog.ReleaseCollectionId)] = ReleaseCollectionId,
            [nameof(EditCollectionMetadataDialog.CollectionName)] = releaseCollection.Name,
            [nameof(EditCollectionMetadataDialog.Metadata)] = releaseCollection.Metadata,
        };

        var dialog = await dialogService.OpenAsync<EditCollectionMetadataDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditMetadata"],
                Description = L["EditMetadataDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (dialog.Cancelled)
        {
            return;
        }

        toastService.Success(L["MetadataUpdated"]);
        await LoadReleaseCollectionAsync();
    }

    private async Task ResolveMetadataAsync()
    {
        if (isResolvingMetadata)
        {
            return;
        }

        isResolvingMetadata = true;

        try
        {
            var resolved = await operationRunner.RunAsync(
                (ReleaseCollectionInfoResolutionService service) =>
                    service.ResolveAsync(ReleaseCollectionId)
            );

            if (resolved)
            {
                toastService.Success(L["SeriesMetadataResolved"]);
                await LoadReleaseCollectionAsync();
            }
            else
            {
                toastService.Info(L["SeriesMetadataNotResolved"]);
            }
        }
        finally
        {
            isResolvingMetadata = false;
        }
    }

    private async Task ShowPostToForumDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(PostToForumDialog.EntityId)] = ReleaseCollectionId,
            [nameof(PostToForumDialog.EntityName)] = releaseCollection.Name,
            [nameof(PostToForumDialog.TemplateType)] = ForumPostTemplateType.ReleaseCollection,
        };

        await dialogService.OpenAsync<PostToForumDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["PostNamedCollectionToForum", releaseCollection.Name],
                Description = L["PostToForumDescription"],
                Size = DialogSize.ExtraLarge,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (postedLocations is not null)
        {
            await postedLocations.ReloadAsync();
        }

        await LoadReleaseCollectionAsync();
    }

    private async Task ShowRenderForumPostDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(RenderForumPostDialog.EntityId)] = ReleaseCollectionId,
            [nameof(RenderForumPostDialog.Type)] = ForumPostTemplateType.ReleaseCollection,
        };

        await dialogService.OpenAsync<RenderForumPostDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["RenderForumPostForCollection", releaseCollection.Name],
                Description = L["RenderForumPostDescription"],
                Size = DialogSize.Full,
                ShowClose = true,
            }
        );
    }

    private async Task DeleteFailedContainerAsync(CollectionUploadSlotContainerReadModel container)
    {
        if (container.State != LinkCrypterContainerState.CreationFailed)
        {
            return;
        }

        var result = await dialogService.ConfirmAsync(
            L["DeleteLinkCrypterContainer"],
            L["DeleteLinkCrypterContainerConfirmation"],
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
            (LinkCrypterContainerService service) =>
                service.DeleteFailedContainerAsync(
                    container.LinkCrypterContainerId,
                    CancellationToken.None
                )
        );
        await LoadReleaseCollectionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

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
