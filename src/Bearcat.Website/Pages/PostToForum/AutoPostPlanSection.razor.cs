using System.Globalization;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.UseCases.PostToForums;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.PostCreation;
using Bearcat.Domain.UseCases.PostToForums.PostUpdate;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.PostToForum;

public partial class AutoPostPlanSection(
    IScopedOperationRunner operationRunner,
    ToastService toastService
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public AutoPostPlan Plan { get; set; } = null!;

    [Parameter]
    public EventCallback<AutoPostPlan> OnPlanChanged { get; set; }

    [Parameter]
    public EventCallback OnUploadsMarkedPosted { get; set; }

    private const string NewThreadValue = "new";

    private readonly HashSet<int> busyRegistrationIds = [];
    private readonly Dictionary<int, string> siteErrors = [];

    private readonly Dictionary<
        int,
        IReadOnlyList<ExistingThread>
    > threadCandidatesByRegistrationId = [];

    private readonly Dictionary<int, string> threadSelectionsByRegistrationId = [];

    private bool IsAnySiteBusy => busyRegistrationIds.Count > 0;

    private bool IsSiteBusy(AutoPostSiteEntry site)
    {
        return busyRegistrationIds.Contains(site.DistributionSiteRegistrationId);
    }

    private static bool CanUpdatePost(AutoPostSiteEntry site)
    {
        return site.PostedLocation is not null && site.ForumPostTemplateIdForUpdate is not null;
    }

    private string? FailureText(AutoPostSiteEntry site)
    {
        var isPending =
            site.Status == AutoPostSiteStatus.Matched
            || (site.Status == AutoPostSiteStatus.AlreadyPosted && site.NeedsContentUpdate);

        if (!isPending)
        {
            return null;
        }

        var failure = site.Failure;

        if (failure?.State == ForumPostingFailureState.RetryScheduled)
        {
            return L[
                "AutoPostPausedAfterFailedAttempts",
                failure.NextAttemptAt!.Value.ToString("g", CultureInfo.CurrentCulture),
                failure.FailedAttemptCount,
                failure.LastErrorMessage!
            ].Value;
        }

        if (failure?.State == ForumPostingFailureState.GaveUp)
        {
            return L[
                "AutoPostGaveUpAfterFailedAttempts",
                failure.FailedAttemptCount,
                failure.LastErrorMessage!
            ].Value;
        }

        if (failure?.State == ForumPostingFailureState.WaitingForThreadSelection)
        {
            return L["AutoPostWaitingForThreadSelection"].Value;
        }

        return null;
    }

    private async Task PostToSiteAsync(AutoPostSiteEntry site)
    {
        busyRegistrationIds.Add(site.DistributionSiteRegistrationId);

        try
        {
            await ExecuteAndApplyAsync(site);
        }
        finally
        {
            busyRegistrationIds.Remove(site.DistributionSiteRegistrationId);
        }
    }

    private async Task PostToAllMatchedSitesAsync()
    {
        var pendingSites = Plan.PendingSites;

        foreach (var site in pendingSites)
        {
            busyRegistrationIds.Add(site.DistributionSiteRegistrationId);
            StateHasChanged();

            try
            {
                var canContinue = await ExecuteAndApplyAsync(site);

                if (!canContinue)
                {
                    return;
                }
            }
            finally
            {
                busyRegistrationIds.Remove(site.DistributionSiteRegistrationId);
            }
        }
    }

    private async Task UpdatePostAsync(AutoPostSiteEntry site)
    {
        var entityType = Plan.EntityType;
        var entityId = Plan.EntityId;

        busyRegistrationIds.Add(site.DistributionSiteRegistrationId);
        siteErrors.Remove(site.DistributionSiteRegistrationId);

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

            if (result.Status == AutoPostResultStatus.Updated)
            {
                toastService.Success(L["AutoPostUpdateSucceeded", site.Registration.Name]);

                if (result.UploadsMarkedPosted)
                {
                    await OnUploadsMarkedPosted.InvokeAsync();
                    return;
                }
            }
            else
            {
                siteErrors[site.DistributionSiteRegistrationId] =
                    result.Status == AutoPostResultStatus.Skipped
                        ? SkipReasonText(entityType, result)
                        : string.Join(" ", result.Errors);

                toastService.Error(L["AutoPostUpdateFailed", site.Registration.Name]);
            }

            await ReloadPlanAsync(entityType, entityId);
        }
        finally
        {
            busyRegistrationIds.Remove(site.DistributionSiteRegistrationId);
        }
    }

    private string GetThreadSelection(AutoPostSiteEntry site)
    {
        return threadSelectionsByRegistrationId.GetValueOrDefault(
            site.DistributionSiteRegistrationId,
            string.Empty
        );
    }

    private void SelectThread(AutoPostSiteEntry site, string threadSelection)
    {
        threadSelectionsByRegistrationId[site.DistributionSiteRegistrationId] = threadSelection;
    }

    private async Task PostToChosenThreadAsync(AutoPostSiteEntry site)
    {
        var plan = Plan;
        var threadSelection = GetThreadSelection(site);
        var threadUrl = threadSelection == NewThreadValue ? null : threadSelection;

        busyRegistrationIds.Add(site.DistributionSiteRegistrationId);
        siteErrors.Remove(site.DistributionSiteRegistrationId);

        try
        {
            var result = await operationRunner.RunAsync(
                (AutoForumPostCreationService service) =>
                    service.CreatePostWithChosenThreadAsync(plan, site, threadUrl)
            );

            await ApplyResultAsync(site, result);
        }
        finally
        {
            busyRegistrationIds.Remove(site.DistributionSiteRegistrationId);
        }
    }

    private async Task<bool> ExecuteAndApplyAsync(AutoPostSiteEntry site)
    {
        var entityType = Plan.EntityType;
        var entityId = Plan.EntityId;

        siteErrors.Remove(site.DistributionSiteRegistrationId);

        var result = await operationRunner.RunAsync(
            (AutoForumPostCreationService service) =>
                service.CreatePostOnSiteAsync(
                    entityType: entityType,
                    entityId: entityId,
                    distributionSiteRegistrationId: site.DistributionSiteRegistrationId
                )
        );

        return await ApplyResultAsync(site, result);
    }

    private async Task<bool> ApplyResultAsync(AutoPostSiteEntry site, AutoPostResult result)
    {
        var entityType = Plan.EntityType;
        var entityId = Plan.EntityId;

        if (result.Status != AutoPostResultStatus.Failed)
        {
            threadCandidatesByRegistrationId.Remove(site.DistributionSiteRegistrationId);
            threadSelectionsByRegistrationId.Remove(site.DistributionSiteRegistrationId);
        }

        if (result.Status == AutoPostResultStatus.ThreadSelectionNeeded)
        {
            threadCandidatesByRegistrationId[site.DistributionSiteRegistrationId] =
                result.ThreadCandidates;

            toastService.Info(L["AutoPostThreadSelectionNeeded", site.Registration.Name]);

            return true;
        }

        if (result.Status == AutoPostResultStatus.Posted)
        {
            toastService.Success(L["AutoPostSucceeded", site.Registration.Name]);

            if (result.UploadsMarkedPosted)
            {
                await OnUploadsMarkedPosted.InvokeAsync();
                return false;
            }

            await ReloadPlanAsync(entityType, entityId);
            return true;
        }

        var message =
            result.Status == AutoPostResultStatus.Skipped
                ? SkipReasonText(entityType, result)
                : string.Join(" ", result.Errors);

        siteErrors[site.DistributionSiteRegistrationId] = message;
        toastService.Error(L["AutoPostFailed", site.Registration.Name]);

        await ReloadPlanAsync(entityType, entityId);

        return false;
    }

    private async Task ReloadPlanAsync(ForumPostTemplateType entityType, int entityId)
    {
        var plan = await operationRunner.RunAsync(
            (AutoForumPostingPlanService service) => service.GetPlanAsync(entityType, entityId)
        );

        await OnPlanChanged.InvokeAsync(plan);
    }

    private string SkipReasonText(ForumPostTemplateType entityType, AutoPostResult result)
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
            AutoPostSkipReason.NotPosted => entityType == ForumPostTemplateType.Release
                ? L["AutoPostUpdateNotPosted"]
                : L["AutoPostUpdateCollectionNotPosted"],
            AutoPostSkipReason.NoMatch => L["AutoPostNoMatchingRule"],
            AutoPostSkipReason.ServiceRestrictionNotMet => L[
                "AutoPostSkippedServiceRestrictionNotMet",
                string.Join(" ", result.Errors)
            ],
            AutoPostSkipReason.ExistingPostOfOtherAuthor => L[
                "AutoPostSkippedExistingPostOfOtherAuthor",
                string.Join(" ", result.ExistingPostUrls)
            ],
            AutoPostSkipReason.SeveralExistingPosts => L[
                "AutoPostSkippedSeveralExistingPosts",
                string.Join(" ", result.ExistingPostUrls)
            ],
            _ => L["AutoPostSkippedSiteUnavailable"],
        };
    }
}
