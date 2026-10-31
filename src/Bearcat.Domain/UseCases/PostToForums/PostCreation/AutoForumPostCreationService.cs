using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.ForumPosting;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.PostToForums.PostCreation;

public class AutoForumPostCreationService(
    AutoForumPostingPlanService planService,
    IAutoForumPostingRepository repository,
    IForumPostContentRenderer contentRenderer,
    IForumPostSubmitter submitter,
    INotificationService notificationService,
    UploadsPostedStatusService uploadsPostedStatusService
)
{
    public async Task<AutoPostExecutionResult> CreatePostOnSiteAsync(
        ForumPostTemplateType entityType,
        int entityId,
        int distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        var plan = await planService.GetPlanAsync(entityType, entityId, cancellationToken);

        if (plan.BlockedReason is not null)
        {
            return AutoPostExecutionResult.Skipped(AutoPostSkipReason.Blocked, plan.BlockedReason);
        }

        var site = plan.FindSite(distributionSiteRegistrationId);

        if (site is null)
        {
            return AutoPostExecutionResult.Skipped(AutoPostSkipReason.SiteNotConfigured);
        }

        return await CreatePostForPlannedSiteAsync(plan, site, cancellationToken);
    }

    public async Task<AutoPostExecutionResult> CreatePostForPlannedSiteAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        CancellationToken cancellationToken = default
    )
    {
        if (site.Status == AutoPostSiteStatus.AlreadyPosted)
        {
            return AutoPostExecutionResult.Skipped(AutoPostSkipReason.AlreadyPosted);
        }

        if (site.Match is null)
        {
            return AutoPostExecutionResult.Skipped(AutoPostSkipReason.NoMatch);
        }

        var result = await CreatePostOrReturnFailureAsync(
            plan: plan,
            site: site,
            match: site.Match,
            cancellationToken: cancellationToken
        );

        await CreatePostResultNotificationAsync(
            entityType: plan.EntityType,
            entityName: plan.EntityName,
            distributionSiteRegistrationName: site.DistributionSiteRegistrationName,
            result: result,
            cancellationToken: cancellationToken
        );

        return result;
    }

    private async Task<AutoPostExecutionResult> CreatePostOrReturnFailureAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        AutoPostRuleMatch match,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await RenderSubmitAndRecordPostAsync(
                plan: plan,
                site: site,
                match: match,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception)
        {
            return AutoPostExecutionResult.Failed([exception.Message]);
        }
    }

    private async Task<AutoPostExecutionResult> RenderSubmitAndRecordPostAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        AutoPostRuleMatch match,
        CancellationToken cancellationToken
    )
    {
        var rendered = await contentRenderer.RenderAsync(
            entityId: plan.EntityId,
            forumPostTemplateId: match.ForumPostTemplateId,
            cancellationToken: cancellationToken
        );

        if (!rendered.IsSuccess)
        {
            return AutoPostExecutionResult.Failed(rendered.Errors);
        }

        var submittedPost = await SubmitReplyToExistingThreadOrNewThreadAsync(
            site: site,
            plan: plan,
            match: match,
            body: rendered.Content,
            cancellationToken: cancellationToken
        );

        await repository.RecordPostedLocationAsync(
            entityType: plan.EntityType,
            entityId: plan.EntityId,
            distributionSiteRegistrationId: site.DistributionSiteRegistrationId,
            url: submittedPost.Url,
            forumPostTemplateId: match.ForumPostTemplateId,
            cancellationToken: cancellationToken
        );

        var uploadsMarkedPosted =
            await uploadsPostedStatusService.MarkUploadsPostedWhenNoSiteIsLeftAsync(
                entityType: plan.EntityType,
                entityId: plan.EntityId,
                cancellationToken: cancellationToken
            );

        return AutoPostExecutionResult.Posted(submittedPost.Url, uploadsMarkedPosted);
    }

    private async Task<SubmittedPost> SubmitReplyToExistingThreadOrNewThreadAsync(
        AutoPostSiteEntry site,
        AutoPostPlan plan,
        AutoPostRuleMatch match,
        string body,
        CancellationToken cancellationToken
    )
    {
        var postName = (
            site.StripDotsForThreadSearch
                ? ReleaseNameFormatter.ToSpacedName(plan.EntityName)
                : plan.EntityName
        ).Trim();

        if (match.PostMode == ForumPostPostMode.ReplyToExistingElseNewThread)
        {
            var existingThreads = await submitter.FindExistingThreadsAsync(
                registrationId: site.DistributionSiteRegistrationId,
                targetNodeId: match.TargetNodeId,
                releaseName: postName,
                cancellationToken: cancellationToken
            );

            if (existingThreads.Count > 0)
            {
                return await submitter.SubmitReplyAsync(
                    registrationId: site.DistributionSiteRegistrationId,
                    threadUrl: existingThreads[0].Url,
                    body: body,
                    cancellationToken: cancellationToken
                );
            }
        }

        return await submitter.SubmitNewThreadAsync(
            registrationId: site.DistributionSiteRegistrationId,
            targetNodeId: match.TargetNodeId,
            title: postName,
            prefixIds: match.ThreadPrefixId is null ? [] : [match.ThreadPrefixId],
            body: body,
            cancellationToken: cancellationToken
        );
    }

    private async Task CreatePostResultNotificationAsync(
        ForumPostTemplateType entityType,
        string entityName,
        string distributionSiteRegistrationName,
        AutoPostExecutionResult result,
        CancellationToken cancellationToken
    )
    {
        var entityLabel =
            entityType == ForumPostTemplateType.Release ? "Release" : "Release collection";

        if (result.Status == AutoPostExecutionStatus.Posted)
        {
            await notificationService.CreateAsync(
                kind: NotificationKind.AutomaticForumPostCreated,
                message: $"{entityLabel} '{entityName}' was posted automatically to '{distributionSiteRegistrationName}': {result.PostedUrl}",
                cancellationToken: cancellationToken
            );

            return;
        }

        await notificationService.CreateAsync(
            kind: NotificationKind.AutomaticForumPostFailed,
            message: $"Automatic posting of {entityLabel.ToLowerInvariant()} '{entityName}' to '{distributionSiteRegistrationName}' failed: {string.Join(" ", result.Errors)}",
            cancellationToken: cancellationToken
        );
    }
}
