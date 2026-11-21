using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Domain.Entities;
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
        return await CreatePostAsync(
            plan: plan,
            site: site,
            shouldSearchMatchingThread: true,
            chosenThreadUrl: null,
            cancellationToken: cancellationToken
        );
    }

    public async Task<AutoPostResult> CreatePostWithChosenThreadAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        string? threadUrl,
        CancellationToken cancellationToken = default
    )
    {
        return await CreatePostAsync(
            plan: plan,
            site: site,
            shouldSearchMatchingThread: false,
            chosenThreadUrl: threadUrl,
            cancellationToken: cancellationToken
        );
    }

    private async Task<AutoPostResult> CreatePostAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        bool shouldSearchMatchingThread,
        string? chosenThreadUrl,
        CancellationToken cancellationToken
    )
    {
        if (site.Status == AutoPostSiteStatus.AlreadyPosted)
        {
            return AutoPostResult.Skipped(AutoPostSkipReason.AlreadyPosted);
        }

        if (site.MatchedRule is null)
        {
            return AutoPostResult.Skipped(AutoPostSkipReason.NoMatch);
        }

        var rule = site.MatchedRule;

        AutoPostResult result;

        try
        {
            result = await RenderAndSubmitPostAsync(
                plan: plan,
                site: site,
                rule: rule,
                shouldSearchMatchingThread: shouldSearchMatchingThread,
                chosenThreadUrl: chosenThreadUrl,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return AutoPostResult.Failed([exception.Message]);
        }

        if (result.Status == AutoPostResultStatus.Posted)
        {
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

        return result;
    }

    private async Task<AutoPostResult> RenderAndSubmitPostAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        ForumPostingRule rule,
        bool shouldSearchMatchingThread,
        string? chosenThreadUrl,
        CancellationToken cancellationToken
    )
    {
        var rendered = await contentRenderer.RenderAsync(
            entityId: plan.EntityId,
            forumPostTemplateId: rule.ForumPostTemplateId,
            distributionSiteRegistrationId: site.DistributionSiteRegistrationId,
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

        if (site.Registration.Kind == DistributionSiteKind.Blog)
        {
            return await SubmitBlogPostAsync(
                plan: plan,
                site: site,
                rule: rule,
                body: rendered.Content,
                coverImageUrl: rendered.CoverImageUrl,
                cancellationToken: cancellationToken
            );
        }

        string? threadUrl;

        if (shouldSearchMatchingThread)
        {
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

            threadUrl = matchingThreads.FirstOrDefault()?.Url;
        }
        else
        {
            threadUrl = chosenThreadUrl;
        }

        if (await IsAlreadyPostedAsync(plan, site, cancellationToken))
        {
            return AutoPostResult.Skipped(AutoPostSkipReason.AlreadyPosted);
        }

        var submittedPost = await SubmitReplyOrNewThreadAsync(
            plan: plan,
            site: site,
            rule: rule,
            threadUrl: threadUrl,
            body: rendered.Content,
            cancellationToken: cancellationToken
        );

        return await RecordPostedLocationAsync(
            plan: plan,
            site: site,
            rule: rule,
            submittedPost: submittedPost,
            cancellationToken: cancellationToken
        );
    }

    private async Task<AutoPostResult> SubmitBlogPostAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        ForumPostingRule rule,
        string body,
        string? coverImageUrl,
        CancellationToken cancellationToken
    )
    {
        if (await IsAlreadyPostedAsync(plan, site, cancellationToken))
        {
            return AutoPostResult.Skipped(AutoPostSkipReason.AlreadyPosted);
        }

        var postName = BuildPostName(plan, site);

        var existingPosts = await submitter.FindExistingPostsAsync(
            registrationId: site.DistributionSiteRegistrationId,
            title: postName,
            cancellationToken: cancellationToken
        );

        var existingPostUrls = existingPosts.Select(post => post.Url).ToList();

        if (existingPosts.Count > 1)
        {
            return AutoPostResult.SkippedForExistingPosts(
                AutoPostSkipReason.SeveralExistingPosts,
                existingPostUrls
            );
        }

        if (existingPosts.Count == 1 && !existingPosts.Single().IsOwnedByAccount)
        {
            return AutoPostResult.SkippedForExistingPosts(
                AutoPostSkipReason.ExistingPostOfOtherAuthor,
                existingPostUrls
            );
        }

        var submittedPost =
            existingPosts.Count == 1
                ? await submitter.PublishExistingPostAsync(
                    registrationId: site.DistributionSiteRegistrationId,
                    postedUrl: existingPosts.Single().Url,
                    body: body,
                    cancellationToken: cancellationToken
                )
                : await submitter.SubmitNewPostAsync(
                    registrationId: site.DistributionSiteRegistrationId,
                    targetNodeId: rule.TargetNodeId,
                    title: postName,
                    body: body,
                    coverImageUrl: coverImageUrl,
                    cancellationToken: cancellationToken
                );

        return await RecordPostedLocationAsync(
            plan: plan,
            site: site,
            rule: rule,
            submittedPost: submittedPost,
            cancellationToken: cancellationToken
        );
    }

    private async Task<AutoPostResult> RecordPostedLocationAsync(
        AutoPostPlan plan,
        AutoPostSiteEntry site,
        ForumPostingRule rule,
        SubmittedPost submittedPost,
        CancellationToken cancellationToken
    )
    {
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

        var postName = BuildPostName(plan, site);

        var existingThreads = await submitter.FindExistingThreadsAsync(
            registrationId: site.DistributionSiteRegistrationId,
            targetNodeId: rule.TargetNodeId,
            releaseName: postName,
            cancellationToken: cancellationToken
        );

        return ForumThreadTitleMatcher.FindMatchingThreads(postName, existingThreads);
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
            title: BuildPostName(plan, site),
            prefixIds: rule.ThreadPrefixIds,
            body: body,
            cancellationToken: cancellationToken
        );
    }

    private static string BuildPostName(AutoPostPlan plan, AutoPostSiteEntry site)
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
