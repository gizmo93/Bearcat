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
    UploadsPostedStatusService uploadsPostedStatusService
)
{
    public async Task<AutoPostResult> UpdatePostOnSiteAsync(
        ForumPostTemplateType entityType,
        int entityId,
        int distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        var plan = await planService.GetPlanAsync(entityType, entityId, cancellationToken);

        if (plan.BlockedReason is not null)
        {
            return AutoPostResult.Skipped(AutoPostSkipReason.Blocked, plan.BlockedReason);
        }

        var site = plan.FindSite(distributionSiteRegistrationId);

        if (site is null)
        {
            return AutoPostResult.Skipped(AutoPostSkipReason.SiteNotConfigured);
        }

        return await UpdatePostOfPlannedSiteAsync(plan, site, cancellationToken);
    }

    public async Task<AutoPostResult> UpdatePostOfPlannedSiteAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        CancellationToken cancellationToken = default
    )
    {
        if (site.PostedLocation is null)
        {
            return AutoPostResult.Skipped(AutoPostSkipReason.NotPosted);
        }

        var target = new AutoPostUpdateTarget(
            PostedLocationId: site.PostedLocation.PostedLocationId,
            EntityType: plan.EntityType,
            EntityId: plan.EntityId,
            EntityName: plan.EntityName,
            DistributionSiteRegistrationId: site.DistributionSiteRegistrationId,
            DistributionSiteRegistrationName: site.Registration.Name,
            PostedUrl: site.PostedLocation.Url,
            ForumPostTemplateId: site.ForumPostTemplateIdForUpdate
        );

        return await UpdatePostAndNotifyAsync(
            target: target,
            forumPostTemplateIdOverride: null,
            cancellationToken: cancellationToken
        );
    }

    public async Task<AutoPostResult> UpdatePostedLocationAsync(
        int postedLocationId,
        int? forumPostTemplateIdOverride = null,
        CancellationToken cancellationToken = default
    )
    {
        var target = await repository.GetUpdateTargetAsync(postedLocationId, cancellationToken);

        return await UpdatePostAndNotifyAsync(
            target: target,
            forumPostTemplateIdOverride: forumPostTemplateIdOverride,
            cancellationToken: cancellationToken
        );
    }

    private async Task<AutoPostResult> UpdatePostAndNotifyAsync(
        AutoPostUpdateTarget target,
        int? forumPostTemplateIdOverride,
        CancellationToken cancellationToken
    )
    {
        var forumPostTemplateId = forumPostTemplateIdOverride ?? target.ForumPostTemplateId;

        if (forumPostTemplateId is null)
        {
            return AutoPostResult.Failed([
                $"No forum post template is known for the post at {target.PostedUrl}; pick one to update it.",
            ]);
        }

        AutoPostResult result;

        try
        {
            result = await RenderEditAndRecordPostAsync(
                target: target,
                forumPostTemplateId: forumPostTemplateId.Value,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return AutoPostResult.Failed([exception.Message]);
        }

        if (result.Status == AutoPostResultStatus.Updated)
        {
            await repository.DeleteForumPostingFailureAsync(
                entityType: target.EntityType,
                entityId: target.EntityId,
                distributionSiteRegistrationId: target.DistributionSiteRegistrationId,
                cancellationToken: cancellationToken
            );

            await notificationService.CreateAsync(
                kind: NotificationKind.AutomaticForumPostUpdated,
                message: $"The post of '{target.EntityName}' in '{target.DistributionSiteRegistrationName}' was updated: {result.PostedUrl}",
                cancellationToken: cancellationToken
            );
        }

        return result;
    }

    private async Task<AutoPostResult> RenderEditAndRecordPostAsync(
        AutoPostUpdateTarget target,
        int forumPostTemplateId,
        CancellationToken cancellationToken
    )
    {
        var rendered = await contentRenderer.RenderAsync(
            entityId: target.EntityId,
            forumPostTemplateId: forumPostTemplateId,
            distributionSiteRegistrationId: target.DistributionSiteRegistrationId,
            cancellationToken: cancellationToken
        );

        if (rendered.BlockedReasons.Count > 0)
        {
            return AutoPostResult.SkippedForServiceRestrictions(rendered.BlockedReasons);
        }

        if (!rendered.IsSuccess)
        {
            return AutoPostResult.Failed(rendered.Errors);
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

        var uploadsMarkedPosted =
            await uploadsPostedStatusService.MarkUploadsPostedWhenNoSiteIsLeftAsync(
                entityType: target.EntityType,
                entityId: target.EntityId,
                cancellationToken: cancellationToken
            );

        return AutoPostResult.Updated(editedPost.Url, uploadsMarkedPosted);
    }
}
