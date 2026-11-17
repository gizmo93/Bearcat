using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.ForumPosting;
using Bearcat.Domain.Shared.ForumPostRendering;
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
        if (FindSkipResult(site) is { } skipResult)
        {
            return skipResult;
        }

        var result = await ReturnFailureOnExceptionAsync(() =>
            SearchMatchingThreadAndCreatePostAsync(
                plan: plan,
                site: site,
                match: site.Match!,
                cancellationToken: cancellationToken
            )
        );

        await CompleteSuccessfulPostAsync(plan, site, result, cancellationToken);

        return result;
    }

    public async Task<AutoPostExecutionResult> CreatePostWithChosenThreadAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        string? threadUrl,
        CancellationToken cancellationToken = default
    )
    {
        if (FindSkipResult(site) is { } skipResult)
        {
            return skipResult;
        }

        var result = await ReturnFailureOnExceptionAsync(() =>
            RenderAndCreatePostInChosenThreadAsync(
                plan: plan,
                site: site,
                match: site.Match!,
                threadUrl: threadUrl,
                cancellationToken: cancellationToken
            )
        );

        await CompleteSuccessfulPostAsync(plan, site, result, cancellationToken);

        return result;
    }

    private static AutoPostExecutionResult? FindSkipResult(AutoPostSiteEntry site)
    {
        if (site.Status == AutoPostSiteStatus.AlreadyPosted)
        {
            return AutoPostExecutionResult.Skipped(AutoPostSkipReason.AlreadyPosted);
        }

        if (site.Match is null)
        {
            return AutoPostExecutionResult.Skipped(AutoPostSkipReason.NoMatch);
        }

        return null;
    }

    private static async Task<AutoPostExecutionResult> ReturnFailureOnExceptionAsync(
        Func<Task<AutoPostExecutionResult>> createPostAsync
    )
    {
        try
        {
            return await createPostAsync();
        }
        catch (Exception exception)
        {
            return AutoPostExecutionResult.Failed([exception.Message]);
        }
    }

    private async Task CompleteSuccessfulPostAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        AutoPostExecutionResult result,
        CancellationToken cancellationToken
    )
    {
        if (result.Status != AutoPostExecutionStatus.Posted)
        {
            return;
        }

        await repository.DeleteForumPostingFailureAsync(
            entityType: plan.EntityType,
            entityId: plan.EntityId,
            distributionSiteRegistrationId: site.DistributionSiteRegistrationId,
            cancellationToken: cancellationToken
        );

        await CreatePostedNotificationAsync(
            plan: plan,
            site: site,
            postedUrl: result.PostedUrl!,
            cancellationToken: cancellationToken
        );
    }

    private async Task<AutoPostExecutionResult> SearchMatchingThreadAndCreatePostAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        AutoPostRuleMatch match,
        CancellationToken cancellationToken
    )
    {
        var rendered = await RenderPostAsync(plan, site, match, cancellationToken);

        if (FindRenderProblemResult(rendered) is { } renderProblemResult)
        {
            return renderProblemResult;
        }

        var matchingThreads = await FindMatchingThreadsAsync(
            plan: plan,
            site: site,
            match: match,
            cancellationToken: cancellationToken
        );

        if (matchingThreads.Count > 1)
        {
            return AutoPostExecutionResult.ThreadSelectionNeeded(matchingThreads);
        }

        return await SubmitAndRecordPostAsync(
            plan: plan,
            site: site,
            match: match,
            threadUrl: matchingThreads.FirstOrDefault()?.Url,
            body: rendered.Content,
            cancellationToken: cancellationToken
        );
    }

    private async Task<AutoPostExecutionResult> RenderAndCreatePostInChosenThreadAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        AutoPostRuleMatch match,
        string? threadUrl,
        CancellationToken cancellationToken
    )
    {
        var rendered = await RenderPostAsync(plan, site, match, cancellationToken);

        if (FindRenderProblemResult(rendered) is { } renderProblemResult)
        {
            return renderProblemResult;
        }

        return await SubmitAndRecordPostAsync(
            plan: plan,
            site: site,
            match: match,
            threadUrl: threadUrl,
            body: rendered.Content,
            cancellationToken: cancellationToken
        );
    }

    private async Task<List<ExistingThread>> FindMatchingThreadsAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        AutoPostRuleMatch match,
        CancellationToken cancellationToken
    )
    {
        if (match.PostMode != ForumPostPostMode.ReplyToExistingElseNewThread)
        {
            return [];
        }

        var postName = PostName(plan, site);

        var existingThreads = await submitter.FindExistingThreadsAsync(
            registrationId: site.DistributionSiteRegistrationId,
            targetNodeId: match.TargetNodeId,
            releaseName: postName,
            cancellationToken: cancellationToken
        );

        return ForumThreadTitleMatcher.FindMatchingThreads(postName, existingThreads);
    }

    private async Task<ForumPostTemplateRenderResult> RenderPostAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        AutoPostRuleMatch match,
        CancellationToken cancellationToken
    )
    {
        return await contentRenderer.RenderAsync(
            entityId: plan.EntityId,
            forumPostTemplateId: match.ForumPostTemplateId,
            distributionSiteRegistrationId: site.DistributionSiteRegistrationId,
            cancellationToken: cancellationToken
        );
    }

    private static AutoPostExecutionResult? FindRenderProblemResult(
        ForumPostTemplateRenderResult rendered
    )
    {
        if (rendered.BlockedReasons.Count > 0)
        {
            return AutoPostExecutionResult.SkippedForServiceRestrictions(rendered.BlockedReasons);
        }

        if (!rendered.IsSuccess)
        {
            return AutoPostExecutionResult.Failed(rendered.Errors);
        }

        return null;
    }

    private async Task<AutoPostExecutionResult> SubmitAndRecordPostAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        AutoPostRuleMatch match,
        string? threadUrl,
        string body,
        CancellationToken cancellationToken
    )
    {
        if (await IsAlreadyPostedAsync(plan, site, cancellationToken))
        {
            return AutoPostExecutionResult.Skipped(AutoPostSkipReason.AlreadyPosted);
        }

        var submittedPost = await SubmitReplyOrNewThreadAsync(
            plan: plan,
            site: site,
            match: match,
            threadUrl: threadUrl,
            body: body,
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

    private async Task<bool> IsAlreadyPostedAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        CancellationToken cancellationToken
    )
    {
        var postedLocations = await repository.GetPostedLocationsAsync(
            entityType: plan.EntityType,
            entityIds: [plan.EntityId],
            cancellationToken: cancellationToken
        );

        return postedLocations.Any(location =>
            location.DistributionSiteRegistrationId == site.DistributionSiteRegistrationId
        );
    }

    private async Task<SubmittedPost> SubmitReplyOrNewThreadAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        AutoPostRuleMatch match,
        string? threadUrl,
        string body,
        CancellationToken cancellationToken
    )
    {
        if (threadUrl is not null)
        {
            return await submitter.SubmitReplyAsync(
                registrationId: site.DistributionSiteRegistrationId,
                threadUrl: threadUrl,
                body: body,
                cancellationToken: cancellationToken
            );
        }

        return await submitter.SubmitNewThreadAsync(
            registrationId: site.DistributionSiteRegistrationId,
            targetNodeId: match.TargetNodeId,
            title: PostName(plan, site),
            prefixIds: match.ThreadPrefixId is null ? [] : [match.ThreadPrefixId],
            body: body,
            cancellationToken: cancellationToken
        );
    }

    private static string PostName(AutoPostPlan plan, AutoPostSiteEntry site)
    {
        var postName = site.StripDotsForThreadSearch
            ? ReleaseNameFormatter.ToSpacedName(plan.EntityName)
            : plan.EntityName;

        return postName.Trim();
    }

    private async Task CreatePostedNotificationAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        string postedUrl,
        CancellationToken cancellationToken
    )
    {
        var entityLabel =
            plan.EntityType == ForumPostTemplateType.Release ? "Release" : "Release collection";

        await notificationService.CreateAsync(
            kind: NotificationKind.AutomaticForumPostCreated,
            message: $"{entityLabel} '{plan.EntityName}' was posted automatically to '{site.DistributionSiteRegistrationName}': {postedUrl}",
            cancellationToken: cancellationToken
        );
    }
}
