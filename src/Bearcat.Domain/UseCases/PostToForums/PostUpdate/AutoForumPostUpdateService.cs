using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.ForumPosting;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.PostToForums.PostUpdate;

public class AutoForumPostUpdateService(
    AutoForumPostingPlanService planService,
    IAutoForumPostingRepository repository,
    IForumPostContentRenderer contentRenderer,
    IForumPostSubmitter submitter,
    INotificationService notificationService,
    ReleasePostedStatusService releasePostedStatusService
)
{
    public async Task<AutoPostUpdateResult> UpdatePostOfReleaseOnSiteAsync(
        int releaseId,
        int distributionSiteRegistrationId,
        int? forumPostTemplateIdOverride = null,
        CancellationToken cancellationToken = default
    )
    {
        var plan = await planService.GetPlanAsync(releaseId, cancellationToken);

        if (plan.BlockedReason is not null)
        {
            return AutoPostUpdateResult.Skipped(
                AutoPostUpdateSkipReason.Blocked,
                plan.BlockedReason
            );
        }

        var site = plan.FindSite(distributionSiteRegistrationId);

        if (site is null)
        {
            return AutoPostUpdateResult.Skipped(AutoPostUpdateSkipReason.SiteNotConfigured);
        }

        return await UpdatePostOfPlannedSiteAsync(
            plan,
            site,
            forumPostTemplateIdOverride,
            cancellationToken
        );
    }

    public async Task<AutoPostUpdateResult> UpdatePostOfPlannedSiteAsync(
        ReleaseAutoPostPlan plan,
        AutoPostSiteEntry site,
        int? forumPostTemplateIdOverride = null,
        CancellationToken cancellationToken = default
    )
    {
        if (site.Status != AutoPostSiteStatus.AlreadyPosted || site.PostedLocationId is null)
        {
            return AutoPostUpdateResult.Skipped(AutoPostUpdateSkipReason.NotPosted);
        }

        var target = new AutoPostUpdateTarget(
            PostedLocationId: site.PostedLocationId.Value,
            EntityId: plan.ReleaseId,
            EntityName: plan.ReleaseName,
            ReleaseId: plan.ReleaseId,
            DistributionSiteRegistrationId: site.DistributionSiteRegistrationId,
            DistributionSiteRegistrationName: site.DistributionSiteRegistrationName,
            PostedUrl: site.PostedUrl!,
            ForumPostTemplateId: site.ResolvedForumPostTemplateId
        );

        return await UpdatePostAndNotifyAsync(
            target,
            forumPostTemplateIdOverride,
            cancellationToken
        );
    }

    public async Task<AutoPostUpdateResult> UpdatePostedLocationAsync(
        int postedLocationId,
        int? forumPostTemplateIdOverride = null,
        CancellationToken cancellationToken = default
    )
    {
        var target = await repository.GetUpdateTargetAsync(postedLocationId, cancellationToken);

        return await UpdatePostAndNotifyAsync(
            target,
            forumPostTemplateIdOverride,
            cancellationToken
        );
    }

    private async Task<AutoPostUpdateResult> UpdatePostAndNotifyAsync(
        AutoPostUpdateTarget target,
        int? forumPostTemplateIdOverride,
        CancellationToken cancellationToken
    )
    {
        var result = await UpdatePostOrReturnFailureAsync(
            target: target,
            forumPostTemplateIdOverride: forumPostTemplateIdOverride,
            cancellationToken: cancellationToken
        );

        await CreateUpdateResultNotificationAsync(target, result, cancellationToken);

        return result;
    }

    private async Task<AutoPostUpdateResult> UpdatePostOrReturnFailureAsync(
        AutoPostUpdateTarget target,
        int? forumPostTemplateIdOverride,
        CancellationToken cancellationToken
    )
    {
        var forumPostTemplateId = forumPostTemplateIdOverride ?? target.ForumPostTemplateId;

        if (forumPostTemplateId is null)
        {
            return AutoPostUpdateResult.Failed([
                $"No forum post template is known for the post at {target.PostedUrl}; pick one to update it.",
            ]);
        }

        try
        {
            return await RenderEditAndRecordPostAsync(
                target: target,
                forumPostTemplateId: forumPostTemplateId.Value,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception)
        {
            return AutoPostUpdateResult.Failed([exception.Message]);
        }
    }

    private async Task<AutoPostUpdateResult> RenderEditAndRecordPostAsync(
        AutoPostUpdateTarget target,
        int forumPostTemplateId,
        CancellationToken cancellationToken
    )
    {
        var rendered = await contentRenderer.RenderAsync(
            entityId: target.EntityId,
            forumPostTemplateId: forumPostTemplateId,
            cancellationToken: cancellationToken
        );

        if (!rendered.IsSuccess)
        {
            return AutoPostUpdateResult.Failed(rendered.Errors);
        }

        var editedPost = await submitter.EditPostAsync(
            registrationId: target.DistributionSiteRegistrationId,
            postedUrl: target.PostedUrl,
            body: rendered.Content,
            cancellationToken: cancellationToken
        );

        await repository.MarkPostedLocationUpdatedAsync(
            postedLocationId: target.PostedLocationId,
            url: editedPost.Url,
            forumPostTemplateId: forumPostTemplateId,
            cancellationToken: cancellationToken
        );

        var releaseMarkedPosted =
            target.ReleaseId is { } releaseId
            && await releasePostedStatusService.MarkReleasePostedWhenNoSiteIsLeftAsync(
                releaseId,
                cancellationToken
            );

        return AutoPostUpdateResult.Updated(editedPost.Url, releaseMarkedPosted);
    }

    private async Task CreateUpdateResultNotificationAsync(
        AutoPostUpdateTarget target,
        AutoPostUpdateResult result,
        CancellationToken cancellationToken
    )
    {
        if (result.Status == AutoPostUpdateStatus.Updated)
        {
            await notificationService.CreateAsync(
                kind: NotificationKind.AutomaticForumPostUpdated,
                message: $"The post of '{target.EntityName}' in '{target.DistributionSiteRegistrationName}' was updated: {result.PostedUrl}",
                cancellationToken: cancellationToken
            );

            return;
        }

        await notificationService.CreateAsync(
            kind: NotificationKind.AutomaticForumPostUpdateFailed,
            message: $"Updating the post of '{target.EntityName}' in '{target.DistributionSiteRegistrationName}' failed: {string.Join(" ", result.Errors)}",
            cancellationToken: cancellationToken
        );
    }
}
