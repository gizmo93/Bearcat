using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Shared.AutoForumPosting;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Website.Formatting;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.PostQueue;

public partial class PostQueuePage(
    IScopedOperationRunner operationRunner,
    PostQueueWorkflowState workflowState,
    NavigationManager navigationManager,
    ToastService toastService,
    TimeProvider timeProvider
) : ComponentBase
{
    private IReadOnlyList<ReleasePostQueueItemReadModel> releaseItems = [];
    private IReadOnlyList<CollectionPostQueueItemReadModel> collectionItems = [];
    private Dictionary<int, ReleaseAutoPostPlan> autoPostPlans = [];
    private readonly HashSet<(int ReleaseId, int RegistrationId)> busySites = [];
    private readonly Dictionary<(int ReleaseId, int RegistrationId), string> siteErrors = [];
    private bool isLoading = true;
    private bool enabled = true;

    protected override async Task OnInitializedAsync()
    {
        enabled = operationRunner.Run(
            (IApplicationConfigurationProvider configuration) =>
                configuration.GetValue<PostQueueConfiguration>(c => c.Enabled)
        );

        if (!enabled)
        {
            isLoading = false;
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;

        try
        {
            await operationRunner.RunAsync(
                async (
                    IReleaseReadRepository releaseRepository,
                    IReleaseCollectionReadRepository collectionRepository
                ) =>
                {
                    releaseItems = await releaseRepository.GetPostQueueAsync();
                    collectionItems = await collectionRepository.GetPostQueueAsync();
                }
            );

            await LoadAutoPostPlansAsync();
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadAutoPostPlansAsync()
    {
        siteErrors.Clear();

        var releaseIds = releaseItems.Select(item => item.ReleaseId).ToList();

        if (releaseIds.Count == 0)
        {
            autoPostPlans = [];
            return;
        }

        var plans = await operationRunner.RunAsync(
            (AutoForumPostingPlanService service) => service.GetPlansAsync(releaseIds)
        );

        autoPostPlans = plans.ToDictionary(plan => plan.ReleaseId);
    }

    private async Task ReloadAutoPostPlanAsync(int releaseId)
    {
        var plan = await operationRunner.RunAsync(
            (AutoForumPostingPlanService service) => service.GetPlanAsync(releaseId)
        );

        autoPostPlans[releaseId] = plan;
    }

    private ReleaseAutoPostPlan AutoPostPlanFor(int releaseId)
    {
        return autoPostPlans[releaseId];
    }

    private bool IsSiteBusy(int releaseId, int registrationId)
    {
        return busySites.Contains((releaseId, registrationId));
    }

    private bool IsReleaseBusy(int releaseId)
    {
        return busySites.Any(site => site.ReleaseId == releaseId);
    }

    private string? SiteError(int releaseId, int registrationId)
    {
        return siteErrors.GetValueOrDefault((releaseId, registrationId));
    }

    private async Task PostToSiteAsync(int releaseId, AutoPostSiteEntry site)
    {
        busySites.Add((releaseId, site.DistributionSiteRegistrationId));

        try
        {
            await ExecuteAndApplyAsync(releaseId, site);
        }
        finally
        {
            busySites.Remove((releaseId, site.DistributionSiteRegistrationId));
        }
    }

    private async Task PostToAllMatchedSitesAsync(ReleaseAutoPostPlan plan)
    {
        var pendingSites = plan.PendingSites;

        foreach (var site in pendingSites)
        {
            busySites.Add((plan.ReleaseId, site.DistributionSiteRegistrationId));
            StateHasChanged();

            try
            {
                var succeeded = await ExecuteAndApplyAsync(plan.ReleaseId, site);

                if (!succeeded)
                {
                    return;
                }
            }
            finally
            {
                busySites.Remove((plan.ReleaseId, site.DistributionSiteRegistrationId));
            }

            if (!autoPostPlans.ContainsKey(plan.ReleaseId))
            {
                return;
            }
        }
    }

    private async Task<bool> ExecuteAndApplyAsync(int releaseId, AutoPostSiteEntry site)
    {
        siteErrors.Remove((releaseId, site.DistributionSiteRegistrationId));

        var result = await operationRunner.RunAsync(
            (AutoForumPostingExecutionService service) =>
                service.ExecuteAsync(releaseId, site.DistributionSiteRegistrationId)
        );

        if (result.Status == AutoPostExecutionStatus.Posted)
        {
            toastService.Success(L["AutoPostSucceeded", site.DistributionSiteRegistrationName]);

            if (result.ReleaseMarkedPosted)
            {
                await LoadAsync();
                return true;
            }

            await ReloadAutoPostPlanAsync(releaseId);
            return true;
        }

        var message =
            result.Status == AutoPostExecutionStatus.Skipped
                ? SkipReasonText(result)
                : string.Join(" ", result.Errors);

        siteErrors[(releaseId, site.DistributionSiteRegistrationId)] = message;
        toastService.Error(L["AutoPostFailed", site.DistributionSiteRegistrationName]);

        await ReloadAutoPostPlanAsync(releaseId);

        return false;
    }

    private string SkipReasonText(AutoPostExecutionResult result)
    {
        if (result.BlockedReason is not null)
        {
            return BlockedReasonText(result.BlockedReason.Value);
        }

        return result.SkipReason switch
        {
            AutoPostSkipReason.AlreadyPosted => L["AutoPostSkippedAlreadyPosted"],
            AutoPostSkipReason.NoMatch => L["AutoPostNoMatchingRule"],
            _ => L["AutoPostSkippedSiteUnavailable"],
        };
    }

    private string BlockedReasonText(AutoPostBlockedReason reason)
    {
        return reason == AutoPostBlockedReason.QualityGateNotPassed
            ? L["AutoPostBlockedQualityGate"]
            : L["AutoPostBlockedClassification"];
    }

    private void StartReleaseWorkflow(int? startReleaseId = null)
    {
        if (releaseItems.Count == 0)
        {
            return;
        }

        var ids = releaseItems.Select(item => item.ReleaseId).ToList();
        workflowState.Start(PostQueueWorkflowType.Release, OrderFrom(ids, startReleaseId));

        navigationManager.NavigateTo($"/releases/{startReleaseId ?? ids[0]}?workflow=postqueue");
    }

    private void StartCollectionWorkflow(int? startCollectionId = null)
    {
        if (collectionItems.Count == 0)
        {
            return;
        }

        var ids = collectionItems.Select(item => item.ReleaseCollectionId).ToList();
        workflowState.Start(PostQueueWorkflowType.Collection, OrderFrom(ids, startCollectionId));

        navigationManager.NavigateTo(
            $"/release-collections/{startCollectionId ?? ids[0]}?workflow=postqueue"
        );
    }

    private async Task MarkReleasePostedAsync(int releaseId)
    {
        await operationRunner.RunAsync(
            (ReleaseService service) => service.MarkUploadsPostedAsync(releaseId)
        );

        await LoadAsync();
    }

    private async Task MarkCollectionPostedAsync(int releaseCollectionId)
    {
        await operationRunner.RunAsync(
            (ReleaseCollectionService service) =>
                service.MarkUploadsPostedAsync(releaseCollectionId)
        );

        await LoadAsync();
    }

    private string HumanizeUploadedAt(DateTime uploadedAt) => timeProvider.Humanize(uploadedAt);

    private static IReadOnlyList<int> OrderFrom(IReadOnlyList<int> ids, int? startId)
    {
        if (startId is null)
        {
            return ids;
        }

        var index = ids.ToList().IndexOf(startId.Value);

        return index <= 0 ? ids : ids.Skip(index).Concat(ids.Take(index)).ToList();
    }
}
