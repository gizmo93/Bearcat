using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Entities;
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
    public async Task<AutoPostResult> CreatePostOnSiteAsync(
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

        return await CreatePostForPlannedSiteAsync(plan, site, cancellationToken);
    }

    public async Task<AutoPostResult> CreatePostForPlannedSiteAsync(
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
                rule: site.MatchedRule!,
                cancellationToken: cancellationToken
            )
        );

        await CompleteSuccessfulPostAsync(plan, site, result, cancellationToken);

        return result;
    }

    public async Task<AutoPostResult> CreatePostWithChosenThreadAsync(
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
                rule: site.MatchedRule!,
                threadUrl: threadUrl,
                cancellationToken: cancellationToken
            )
        );

        await CompleteSuccessfulPostAsync(plan, site, result, cancellationToken);

        return result;
    }

    private static AutoPostResult? FindSkipResult(AutoPostSiteEntry site)
    {
        if (site.Status == AutoPostSiteStatus.AlreadyPosted)
        {
            return AutoPostResult.Skipped(AutoPostSkipReason.AlreadyPosted);
        }

        if (site.MatchedRule is null)
        {
            return AutoPostResult.Skipped(AutoPostSkipReason.NoMatch);
        }

        return null;
    }

    private static async Task<AutoPostResult> ReturnFailureOnExceptionAsync(
        Func<Task<AutoPostResult>> createPostAsync
    )
    {
        try
        {
            return await createPostAsync();
        }
        catch (Exception exception)
        {
            return AutoPostResult.Failed([exception.Message]);
        }
    }

    private async Task CompleteSuccessfulPostAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        AutoPostResult result,
        CancellationToken cancellationToken
    )
    {
        if (result.Status != AutoPostResultStatus.Posted)
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

    private async Task<AutoPostResult> SearchMatchingThreadAndCreatePostAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        ForumPostingRule rule,
        CancellationToken cancellationToken
    )
    {
        var rendered = await RenderPostAsync(plan, site, rule, cancellationToken);

        if (FindRenderProblemResult(rendered) is { } renderProblemResult)
        {
            return renderProblemResult;
        }

        var matchingThreads = await FindMatchingThreadsAsync(
            plan: plan,
            site: site,
            rule: rule,
            cancellationToken: cancellationToken
        );

        if (matchingThreads.Count > 1)
        {
            return AutoPostResult.ThreadSelectionNeeded(matchingThreads);
        }

        return await SubmitAndRecordPostAsync(
            plan: plan,
            site: site,
            rule: rule,
            threadUrl: matchingThreads.FirstOrDefault()?.Url,
            body: rendered.Content,
            cancellationToken: cancellationToken
        );
    }

    private async Task<AutoPostResult> RenderAndCreatePostInChosenThreadAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        ForumPostingRule rule,
        string? threadUrl,
        CancellationToken cancellationToken
    )
    {
        var rendered = await RenderPostAsync(plan, site, rule, cancellationToken);

        if (FindRenderProblemResult(rendered) is { } renderProblemResult)
        {
            return renderProblemResult;
        }

        return await SubmitAndRecordPostAsync(
            plan: plan,
            site: site,
            rule: rule,
            threadUrl: threadUrl,
            body: rendered.Content,
            cancellationToken: cancellationToken
        );
    }

    private async Task<List<ExistingThread>> FindMatchingThreadsAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        ForumPostingRule rule,
        CancellationToken cancellationToken
    )
    {
        if (rule.PostMode != ForumPostPostMode.ReplyToExistingElseNewThread)
        {
            return [];
        }

        var postName = PostName(plan, site);

        var existingThreads = await submitter.FindExistingThreadsAsync(
            registrationId: site.DistributionSiteRegistrationId,
            targetNodeId: rule.TargetNodeId,
            releaseName: postName,
            cancellationToken: cancellationToken
        );

        return ForumThreadTitleMatcher.FindMatchingThreads(postName, existingThreads);
    }

    private async Task<ForumPostTemplateRenderResult> RenderPostAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        ForumPostingRule rule,
        CancellationToken cancellationToken
    )
    {
        return await contentRenderer.RenderAsync(
            entityId: plan.EntityId,
            forumPostTemplateId: rule.ForumPostTemplateId,
            distributionSiteRegistrationId: site.DistributionSiteRegistrationId,
            cancellationToken: cancellationToken
        );
    }

    private static AutoPostResult? FindRenderProblemResult(ForumPostTemplateRenderResult rendered)
    {
        if (rendered.BlockedReasons.Count > 0)
        {
            return AutoPostResult.SkippedForServiceRestrictions(rendered.BlockedReasons);
        }

        if (!rendered.IsSuccess)
        {
            return AutoPostResult.Failed(rendered.Errors);
        }

        return null;
    }

    private async Task<AutoPostResult> SubmitAndRecordPostAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        ForumPostingRule rule,
        string? threadUrl,
        string body,
        CancellationToken cancellationToken
    )
    {
        if (await IsAlreadyPostedAsync(plan, site, cancellationToken))
        {
            return AutoPostResult.Skipped(AutoPostSkipReason.AlreadyPosted);
        }

        var submittedPost = await SubmitReplyOrNewThreadAsync(
            plan: plan,
            site: site,
            rule: rule,
            threadUrl: threadUrl,
            body: body,
            cancellationToken: cancellationToken
        );

        await repository.RecordPostedLocationAsync(
            entityType: plan.EntityType,
            entityId: plan.EntityId,
            distributionSiteRegistrationId: site.DistributionSiteRegistrationId,
            url: submittedPost.Url,
            forumPostTemplateId: rule.ForumPostTemplateId,
            cancellationToken: cancellationToken
        );

        var uploadsMarkedPosted =
            await uploadsPostedStatusService.MarkUploadsPostedWhenNoSiteIsLeftAsync(
                entityType: plan.EntityType,
                entityId: plan.EntityId,
                cancellationToken: cancellationToken
            );

        return AutoPostResult.Posted(submittedPost.Url, uploadsMarkedPosted);
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
        ForumPostingRule rule,
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
            targetNodeId: rule.TargetNodeId,
            title: PostName(plan, site),
            prefixIds: rule.ThreadPrefixId is null ? [] : [rule.ThreadPrefixId],
            body: body,
            cancellationToken: cancellationToken
        );
    }

    private static string PostName(AutoPostPlan plan, AutoPostSiteEntry site)
    {
        var postName = site.Registration.StripDotsForThreadSearch
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
            message: $"{entityLabel} '{plan.EntityName}' was posted automatically to '{site.Registration.Name}': {postedUrl}",
            cancellationToken: cancellationToken
        );
    }
}
