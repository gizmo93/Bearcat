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
    ReleasePostedStatusService releasePostedStatusService
)
{
    public async Task<AutoPostExecutionResult> CreatePostForReleaseOnSiteAsync(
        int releaseId,
        int distributionSiteRegistrationId,
        CancellationToken cancellationToken = default
    )
    {
        var plan = await planService.GetPlanAsync(releaseId, cancellationToken);

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
        ReleaseAutoPostPlan plan,
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
            releaseName: plan.ReleaseName,
            distributionSiteRegistrationName: site.DistributionSiteRegistrationName,
            result: result,
            cancellationToken: cancellationToken
        );

        return result;
    }

    private async Task<AutoPostExecutionResult> CreatePostOrReturnFailureAsync(
        ReleaseAutoPostPlan plan,
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
        ReleaseAutoPostPlan plan,
        AutoPostSiteEntry site,
        AutoPostRuleMatch match,
        CancellationToken cancellationToken
    )
    {
        var rendered = await contentRenderer.RenderAsync(
            entityId: plan.ReleaseId,
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
            releaseId: plan.ReleaseId,
            distributionSiteRegistrationId: site.DistributionSiteRegistrationId,
            url: submittedPost.Url,
            forumPostTemplateId: match.ForumPostTemplateId,
            cancellationToken: cancellationToken
        );

        var releaseMarkedPosted =
            await releasePostedStatusService.MarkReleasePostedWhenNoSiteIsLeftAsync(
                plan.ReleaseId,
                cancellationToken
            );

        return AutoPostExecutionResult.Posted(submittedPost.Url, releaseMarkedPosted);
    }

    private async Task<SubmittedPost> SubmitReplyToExistingThreadOrNewThreadAsync(
        AutoPostSiteEntry site,
        ReleaseAutoPostPlan plan,
        AutoPostRuleMatch match,
        string body,
        CancellationToken cancellationToken
    )
    {
        var postName = (
            site.StripDotsForThreadSearch
                ? ReleaseNameFormatter.ToSpacedName(plan.ReleaseName)
                : plan.ReleaseName
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
        string releaseName,
        string distributionSiteRegistrationName,
        AutoPostExecutionResult result,
        CancellationToken cancellationToken
    )
    {
        if (result.Status == AutoPostExecutionStatus.Posted)
        {
            await notificationService.CreateAsync(
                kind: NotificationKind.AutomaticForumPostCreated,
                message: $"Release '{releaseName}' was posted automatically to '{distributionSiteRegistrationName}': {result.PostedUrl}",
                cancellationToken: cancellationToken
            );

            return;
        }

        await notificationService.CreateAsync(
            kind: NotificationKind.AutomaticForumPostFailed,
            message: $"Automatic posting of release '{releaseName}' to '{distributionSiteRegistrationName}' failed: {string.Join(" ", result.Errors)}",
            cancellationToken: cancellationToken
        );
    }
}
