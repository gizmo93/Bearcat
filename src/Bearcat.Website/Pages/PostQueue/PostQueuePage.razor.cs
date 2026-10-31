using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Shared.PostQueue;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.PostCreation;
using Bearcat.Domain.UseCases.PostToForums.PostUpdate;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Formatting;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.PostQueue;

public partial class PostQueuePage(
    IScopedOperationRunner operationRunner,
    PostQueueWorkflowState workflowState,
    PostQueueCountCache postQueueCountCache,
    NavigationManager navigationManager,
    ToastService toastService,
    TimeProvider timeProvider
) : ComponentBase
{
    private IReadOnlyList<ReleasePostQueueItemReadModel> releaseItems = [];
    private IReadOnlyList<CollectionPostQueueItemReadModel> collectionItems = [];
    private Dictionary<
        (ForumPostTemplateType EntityType, int EntityId),
        AutoPostPlan
    > autoPostPlans = [];
    private readonly HashSet<(
        ForumPostTemplateType EntityType,
        int EntityId,
        int RegistrationId
    )> busySites = [];
    private readonly Dictionary<
        (ForumPostTemplateType EntityType, int EntityId, int RegistrationId),
        string
    > siteErrors = [];
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
        postQueueCountCache.Invalidate();

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

        var releasePlans = await LoadAutoPostPlansAsync(
            ForumPostTemplateType.Release,
            releaseItems.Select(item => item.ReleaseId).ToList()
        );
        var collectionPlans = await LoadAutoPostPlansAsync(
            ForumPostTemplateType.ReleaseCollection,
            collectionItems.Select(item => item.ReleaseCollectionId).ToList()
        );

        autoPostPlans = releasePlans
            .Concat(collectionPlans)
            .ToDictionary(plan => (plan.EntityType, plan.EntityId));
    }

    private async Task<IReadOnlyList<AutoPostPlan>> LoadAutoPostPlansAsync(
        ForumPostTemplateType entityType,
        List<int> entityIds
    )
    {
        if (entityIds.Count == 0)
        {
            return [];
        }

        return await operationRunner.RunAsync(
            (AutoForumPostingPlanService service) => service.GetPlansAsync(entityType, entityIds)
        );
    }

    private async Task ReloadAutoPostPlanAsync(ForumPostTemplateType entityType, int entityId)
    {
        var plan = await operationRunner.RunAsync(
            (AutoForumPostingPlanService service) => service.GetPlanAsync(entityType, entityId)
        );

        autoPostPlans[(entityType, entityId)] = plan;
    }

    private AutoPostPlan AutoPostPlanFor(ForumPostTemplateType entityType, int entityId)
    {
        return autoPostPlans[(entityType, entityId)];
    }

    private HashSet<int> BusyRegistrationIdsFor(ForumPostTemplateType entityType, int entityId)
    {
        return busySites
            .Where(site => site.EntityType == entityType && site.EntityId == entityId)
            .Select(site => site.RegistrationId)
            .ToHashSet();
    }

    private Dictionary<int, string> SiteErrorsFor(ForumPostTemplateType entityType, int entityId)
    {
        return siteErrors
            .Where(siteError =>
                siteError.Key.EntityType == entityType && siteError.Key.EntityId == entityId
            )
            .ToDictionary(siteError => siteError.Key.RegistrationId, siteError => siteError.Value);
    }

    private async Task PostToSiteAsync(
        ForumPostTemplateType entityType,
        int entityId,
        AutoPostSiteEntry site
    )
    {
        busySites.Add((entityType, entityId, site.DistributionSiteRegistrationId));

        try
        {
            await ExecuteAndApplyAsync(entityType, entityId, site);
        }
        finally
        {
            busySites.Remove((entityType, entityId, site.DistributionSiteRegistrationId));
        }
    }

    private async Task PostToAllMatchedSitesAsync(ForumPostTemplateType entityType, int entityId)
    {
        var pendingSites = AutoPostPlanFor(entityType, entityId).PendingSites;

        foreach (var site in pendingSites)
        {
            busySites.Add((entityType, entityId, site.DistributionSiteRegistrationId));
            StateHasChanged();

            try
            {
                var succeeded = await ExecuteAndApplyAsync(entityType, entityId, site);

                if (!succeeded)
                {
                    return;
                }
            }
            finally
            {
                busySites.Remove((entityType, entityId, site.DistributionSiteRegistrationId));
            }

            if (!autoPostPlans.ContainsKey((entityType, entityId)))
            {
                return;
            }
        }
    }

    private async Task UpdatePostAsync(
        ForumPostTemplateType entityType,
        int entityId,
        AutoPostSiteEntry site
    )
    {
        busySites.Add((entityType, entityId, site.DistributionSiteRegistrationId));
        siteErrors.Remove((entityType, entityId, site.DistributionSiteRegistrationId));

        try
        {
            var result = await operationRunner.RunAsync(
                (AutoForumPostUpdateService service) =>
                    service.UpdatePostOnSiteAsync(
                        entityType: entityType,
                        entityId: entityId,
                        distributionSiteRegistrationId: site.DistributionSiteRegistrationId
                    )
            );

            if (result.Status == AutoPostUpdateStatus.Updated)
            {
                toastService.Success(
                    L["AutoPostUpdateSucceeded", site.DistributionSiteRegistrationName]
                );

                if (result.UploadsMarkedPosted)
                {
                    await LoadAsync();
                    return;
                }
            }
            else
            {
                siteErrors[(entityType, entityId, site.DistributionSiteRegistrationId)] =
                    result.Status == AutoPostUpdateStatus.Skipped
                        ? UpdateSkipReasonText(entityType, result)
                        : string.Join(" ", result.Errors);

                toastService.Error(
                    L["AutoPostUpdateFailed", site.DistributionSiteRegistrationName]
                );
            }

            await ReloadAutoPostPlanAsync(entityType, entityId);
        }
        finally
        {
            busySites.Remove((entityType, entityId, site.DistributionSiteRegistrationId));
        }
    }

    private async Task<bool> ExecuteAndApplyAsync(
        ForumPostTemplateType entityType,
        int entityId,
        AutoPostSiteEntry site
    )
    {
        siteErrors.Remove((entityType, entityId, site.DistributionSiteRegistrationId));

        var result = await operationRunner.RunAsync(
            (AutoForumPostCreationService service) =>
                service.CreatePostOnSiteAsync(
                    entityType: entityType,
                    entityId: entityId,
                    distributionSiteRegistrationId: site.DistributionSiteRegistrationId
                )
        );

        if (result.Status == AutoPostExecutionStatus.Posted)
        {
            toastService.Success(L["AutoPostSucceeded", site.DistributionSiteRegistrationName]);

            if (result.UploadsMarkedPosted)
            {
                await LoadAsync();
                return true;
            }

            await ReloadAutoPostPlanAsync(entityType, entityId);
            return true;
        }

        var message =
            result.Status == AutoPostExecutionStatus.Skipped
                ? SkipReasonText(entityType, result)
                : string.Join(" ", result.Errors);

        siteErrors[(entityType, entityId, site.DistributionSiteRegistrationId)] = message;
        toastService.Error(L["AutoPostFailed", site.DistributionSiteRegistrationName]);

        await ReloadAutoPostPlanAsync(entityType, entityId);

        return false;
    }

    private string SkipReasonText(ForumPostTemplateType entityType, AutoPostExecutionResult result)
    {
        if (result.BlockedReason is not null)
        {
            return L.Localize(result.BlockedReason.Value, entityType);
        }

        return result.SkipReason switch
        {
            AutoPostSkipReason.AlreadyPosted => entityType == ForumPostTemplateType.Release
                ? L["AutoPostSkippedAlreadyPosted"]
                : L["AutoPostSkippedCollectionAlreadyPosted"],
            AutoPostSkipReason.NoMatch => L["AutoPostNoMatchingRule"],
            _ => L["AutoPostSkippedSiteUnavailable"],
        };
    }

    private string UpdateSkipReasonText(
        ForumPostTemplateType entityType,
        AutoPostUpdateResult result
    )
    {
        if (result.BlockedReason is not null)
        {
            return L.Localize(result.BlockedReason.Value, entityType);
        }

        if (result.SkipReason != AutoPostUpdateSkipReason.NotPosted)
        {
            return L["AutoPostSkippedSiteUnavailable"];
        }

        return entityType == ForumPostTemplateType.Release
            ? L["AutoPostUpdateNotPosted"]
            : L["AutoPostUpdateCollectionNotPosted"];
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
