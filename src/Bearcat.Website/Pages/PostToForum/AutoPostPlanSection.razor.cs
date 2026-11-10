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

    private readonly HashSet<int> busyRegistrationIds = [];
    private readonly Dictionary<int, string> siteErrors = [];

    private bool IsAnySiteBusy => busyRegistrationIds.Count > 0;

    private bool IsSiteBusy(AutoPostSiteEntry site)
    {
        return busyRegistrationIds.Contains(site.DistributionSiteRegistrationId);
    }

    private static bool CanUpdatePost(AutoPostSiteEntry site)
    {
        return site.PostedLocationId is not null && site.ResolvedForumPostTemplateId is not null;
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

            if (result.Status == AutoPostUpdateStatus.Updated)
            {
                toastService.Success(
                    L["AutoPostUpdateSucceeded", site.DistributionSiteRegistrationName]
                );

                if (result.UploadsMarkedPosted)
                {
                    await OnUploadsMarkedPosted.InvokeAsync();
                    return;
                }
            }
            else
            {
                siteErrors[site.DistributionSiteRegistrationId] =
                    result.Status == AutoPostUpdateStatus.Skipped
                        ? UpdateSkipReasonText(entityType, result)
                        : string.Join(" ", result.Errors);

                toastService.Error(
                    L["AutoPostUpdateFailed", site.DistributionSiteRegistrationName]
                );
            }

            await ReloadPlanAsync(entityType, entityId);
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

        if (result.Status == AutoPostExecutionStatus.Posted)
        {
            toastService.Success(L["AutoPostSucceeded", site.DistributionSiteRegistrationName]);

            if (result.UploadsMarkedPosted)
            {
                await OnUploadsMarkedPosted.InvokeAsync();
                return false;
            }

            await ReloadPlanAsync(entityType, entityId);
            return true;
        }

        var message =
            result.Status == AutoPostExecutionStatus.Skipped
                ? SkipReasonText(entityType, result)
                : string.Join(" ", result.Errors);

        siteErrors[site.DistributionSiteRegistrationId] = message;
        toastService.Error(L["AutoPostFailed", site.DistributionSiteRegistrationName]);

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
}
