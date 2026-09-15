using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.AutoForumPosting;

public class AutoForumPostingExecutionService(
    AutoForumPostingPlanService planService,
    IAutoForumPostingRepository repository,
    IForumPostContentRenderer contentRenderer,
    IForumPostSubmitter submitter
)
{
    public async Task<AutoPostExecutionResult> ExecuteAsync(
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

        var entry = plan.Sites.FirstOrDefault(site =>
            site.DistributionSiteRegistrationId == distributionSiteRegistrationId
        );

        if (entry is null)
        {
            return AutoPostExecutionResult.Skipped(AutoPostSkipReason.SiteNotConfigured);
        }

        if (entry.Status == AutoPostSiteStatus.AlreadyPosted)
        {
            return AutoPostExecutionResult.Skipped(AutoPostSkipReason.AlreadyPosted);
        }

        if (entry.Match is null)
        {
            return AutoPostExecutionResult.Skipped(AutoPostSkipReason.NoMatch);
        }

        try
        {
            return await PostAsync(
                plan: plan,
                registrationId: distributionSiteRegistrationId,
                match: entry.Match,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception)
        {
            return AutoPostExecutionResult.Failed([exception.Message]);
        }
    }

    private async Task<AutoPostExecutionResult> PostAsync(
        ReleaseAutoPostPlan plan,
        int registrationId,
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

        var submittedPost = await SubmitAsync(
            registrationId: registrationId,
            plan: plan,
            match: match,
            body: rendered.Content,
            cancellationToken: cancellationToken
        );

        await repository.RecordPostedLocationAsync(
            releaseId: plan.ReleaseId,
            distributionSiteRegistrationId: registrationId,
            url: submittedPost.Url,
            cancellationToken: cancellationToken
        );

        var releaseMarkedPosted = await MarkReleasePostedWhenDoneAsync(
            plan.ReleaseId,
            cancellationToken
        );

        return AutoPostExecutionResult.Posted(submittedPost.Url, releaseMarkedPosted);
    }

    private async Task<SubmittedPost> SubmitAsync(
        int registrationId,
        ReleaseAutoPostPlan plan,
        AutoPostRuleMatch match,
        string body,
        CancellationToken cancellationToken
    )
    {
        if (match.PostMode == ForumPostPostMode.ReplyToExistingElseNewThread)
        {
            var existingThreads = await submitter.FindExistingThreadsAsync(
                registrationId: registrationId,
                targetNodeId: match.TargetNodeId,
                releaseName: plan.ReleaseName,
                cancellationToken: cancellationToken
            );

            if (existingThreads.Count > 0)
            {
                return await submitter.SubmitReplyAsync(
                    registrationId: registrationId,
                    threadUrl: existingThreads[0].Url,
                    body: body,
                    cancellationToken: cancellationToken
                );
            }
        }

        return await submitter.SubmitNewThreadAsync(
            registrationId: registrationId,
            targetNodeId: match.TargetNodeId,
            title: plan.ReleaseName,
            prefixIds: match.ThreadPrefixId is null ? [] : [match.ThreadPrefixId],
            body: body,
            cancellationToken: cancellationToken
        );
    }

    private async Task<bool> MarkReleasePostedWhenDoneAsync(
        int releaseId,
        CancellationToken cancellationToken
    )
    {
        var freshPlan = await planService.GetPlanAsync(releaseId, cancellationToken);

        if (freshPlan.IsBlocked || freshPlan.PendingSites.Count > 0)
        {
            return false;
        }

        await repository.MarkReleasePostedAsync(releaseId, cancellationToken);

        return true;
    }
}
