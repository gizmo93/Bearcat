using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.ForumPosting;
using Bearcat.Domain.UseCases.PostToForums.Models;
using Bearcat.Domain.UseCases.PostToForums.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.PostToForums;

public class AutoForumPostingService(
    AutoForumPostingPlanService planService,
    IAutoForumPostingRepository repository,
    IForumPostContentRenderer contentRenderer,
    IForumPostSubmitter submitter,
    INotificationService notificationService
)
{
    public async Task<AutoPostExecutionResult> PostAsync(
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

        var entry = FindSite(plan, distributionSiteRegistrationId);

        if (entry is null)
        {
            return AutoPostExecutionResult.Skipped(AutoPostSkipReason.SiteNotConfigured);
        }

        return await PostAsync(plan, entry, cancellationToken);
    }

    public async Task<AutoPostExecutionResult> PostAsync(
        ReleaseAutoPostPlan plan,
        AutoPostSiteEntry entry,
        CancellationToken cancellationToken = default
    )
    {
        if (entry.Status == AutoPostSiteStatus.AlreadyPosted)
        {
            return AutoPostExecutionResult.Skipped(AutoPostSkipReason.AlreadyPosted);
        }

        if (entry.Match is null)
        {
            return AutoPostExecutionResult.Skipped(AutoPostSkipReason.NoMatch);
        }

        var result = await TryCreatePostAsync(
            plan: plan,
            entry: entry,
            match: entry.Match,
            cancellationToken: cancellationToken
        );

        await NotifyPostedAsync(
            releaseName: plan.ReleaseName,
            distributionSiteRegistrationName: entry.DistributionSiteRegistrationName,
            result: result,
            cancellationToken: cancellationToken
        );

        return result;
    }

    public async Task<AutoPostUpdateResult> UpdateAsync(
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

        var entry = FindSite(plan, distributionSiteRegistrationId);

        if (entry is null)
        {
            return AutoPostUpdateResult.Skipped(AutoPostUpdateSkipReason.SiteNotConfigured);
        }

        return await UpdateAsync(plan, entry, forumPostTemplateIdOverride, cancellationToken);
    }

    public async Task<AutoPostUpdateResult> UpdateAsync(
        ReleaseAutoPostPlan plan,
        AutoPostSiteEntry entry,
        int? forumPostTemplateIdOverride = null,
        CancellationToken cancellationToken = default
    )
    {
        if (entry.Status != AutoPostSiteStatus.AlreadyPosted || entry.PostedLocationId is null)
        {
            return AutoPostUpdateResult.Skipped(AutoPostUpdateSkipReason.NotPosted);
        }

        var target = new AutoPostUpdateTarget(
            PostedLocationId: entry.PostedLocationId.Value,
            EntityId: plan.ReleaseId,
            EntityName: plan.ReleaseName,
            ReleaseId: plan.ReleaseId,
            DistributionSiteRegistrationId: entry.DistributionSiteRegistrationId,
            DistributionSiteRegistrationName: entry.DistributionSiteRegistrationName,
            PostedUrl: entry.PostedUrl!,
            ForumPostTemplateId: entry.ResolvedForumPostTemplateId
        );

        return await UpdateAsync(target, forumPostTemplateIdOverride, cancellationToken);
    }

    public async Task<AutoPostUpdateResult> UpdatePostedLocationAsync(
        int postedLocationId,
        int? forumPostTemplateIdOverride = null,
        CancellationToken cancellationToken = default
    )
    {
        var target = await repository.GetUpdateTargetAsync(postedLocationId, cancellationToken);

        return await UpdateAsync(target, forumPostTemplateIdOverride, cancellationToken);
    }

    private static AutoPostSiteEntry? FindSite(
        ReleaseAutoPostPlan plan,
        int distributionSiteRegistrationId
    )
    {
        return plan.Sites.FirstOrDefault(site =>
            site.DistributionSiteRegistrationId == distributionSiteRegistrationId
        );
    }

    private async Task<AutoPostExecutionResult> TryCreatePostAsync(
        ReleaseAutoPostPlan plan,
        AutoPostSiteEntry entry,
        AutoPostRuleMatch match,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await CreatePostAsync(
                plan: plan,
                entry: entry,
                match: match,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception)
        {
            return AutoPostExecutionResult.Failed([exception.Message]);
        }
    }

    private async Task<AutoPostExecutionResult> CreatePostAsync(
        ReleaseAutoPostPlan plan,
        AutoPostSiteEntry entry,
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
            entry: entry,
            plan: plan,
            match: match,
            body: rendered.Content,
            cancellationToken: cancellationToken
        );

        await repository.RecordPostedLocationAsync(
            releaseId: plan.ReleaseId,
            distributionSiteRegistrationId: entry.DistributionSiteRegistrationId,
            url: submittedPost.Url,
            forumPostTemplateId: match.ForumPostTemplateId,
            cancellationToken: cancellationToken
        );

        var releaseMarkedPosted = await MarkReleasePostedWhenDoneAsync(
            plan.ReleaseId,
            cancellationToken
        );

        return AutoPostExecutionResult.Posted(submittedPost.Url, releaseMarkedPosted);
    }

    private async Task<bool> MarkReleasePostedWhenDoneAsync(
        int releaseId,
        CancellationToken cancellationToken
    )
    {
        var freshPlan = await planService.GetPlanAsync(releaseId, cancellationToken);

        if (
            freshPlan.IsBlocked
            || freshPlan.PendingSites.Count > 0
            || freshPlan.UpdatableSites.Count > 0
        )
        {
            return false;
        }

        await repository.MarkReleasePostedAsync(releaseId, cancellationToken);

        return true;
    }

    private async Task<SubmittedPost> SubmitAsync(
        AutoPostSiteEntry entry,
        ReleaseAutoPostPlan plan,
        AutoPostRuleMatch match,
        string body,
        CancellationToken cancellationToken
    )
    {
        var postName = (
            entry.StripDotsForThreadSearch
                ? ReleaseNameFormatter.ToSpacedName(plan.ReleaseName)
                : plan.ReleaseName
        ).Trim();

        if (match.PostMode == ForumPostPostMode.ReplyToExistingElseNewThread)
        {
            var existingThreads = await submitter.FindExistingThreadsAsync(
                registrationId: entry.DistributionSiteRegistrationId,
                targetNodeId: match.TargetNodeId,
                releaseName: postName,
                cancellationToken: cancellationToken
            );

            if (existingThreads.Count > 0)
            {
                return await submitter.SubmitReplyAsync(
                    registrationId: entry.DistributionSiteRegistrationId,
                    threadUrl: existingThreads[0].Url,
                    body: body,
                    cancellationToken: cancellationToken
                );
            }
        }

        return await submitter.SubmitNewThreadAsync(
            registrationId: entry.DistributionSiteRegistrationId,
            targetNodeId: match.TargetNodeId,
            title: postName,
            prefixIds: match.ThreadPrefixId is null ? [] : [match.ThreadPrefixId],
            body: body,
            cancellationToken: cancellationToken
        );
    }

    private async Task<AutoPostUpdateResult> UpdateAsync(
        AutoPostUpdateTarget target,
        int? forumPostTemplateIdOverride,
        CancellationToken cancellationToken
    )
    {
        var result = await TryUpdatePostAsync(
            target: target,
            forumPostTemplateIdOverride: forumPostTemplateIdOverride,
            cancellationToken: cancellationToken
        );

        await NotifyUpdatedAsync(target, result, cancellationToken);

        return result;
    }

    private async Task<AutoPostUpdateResult> TryUpdatePostAsync(
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
            return await UpdatePostAsync(
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

    private async Task<AutoPostUpdateResult> UpdatePostAsync(
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
            && await MarkReleasePostedWhenDoneAsync(releaseId, cancellationToken);

        return AutoPostUpdateResult.Updated(editedPost.Url, releaseMarkedPosted);
    }

    private async Task NotifyPostedAsync(
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

    private async Task NotifyUpdatedAsync(
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
