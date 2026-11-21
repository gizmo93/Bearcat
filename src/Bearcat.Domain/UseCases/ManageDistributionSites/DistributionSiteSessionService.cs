using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Abstractions.DistributionSite.Results;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ForumPosting;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;

namespace Bearcat.Domain.UseCases.ManageDistributionSites;

public class DistributionSiteSessionService(
    IDistributionSiteRegistrationWriteRepository repository,
    IDistributionSessionRepository sessionRepository,
    IDistributionSiteFactory distributionSiteFactory,
    ISecretProtector secretProtector
) : IForumPostSubmitter
{
    public async Task<TryLoginResult> TestLoginAsync(
        int registrationId,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetByIdAsync(registrationId, cancellationToken);
        var site = distributionSiteFactory.GetByClassName(registration.DistributionSiteClassName);

        var loginResult = await site.LogInAsync(
            DeserializeConfig(site, registration),
            cancellationToken
        );

        if (loginResult.Session is null)
        {
            return new TryLoginResult(IsSuccess: false, ErrorMessage: loginResult.ErrorMessage);
        }

        await sessionRepository.SaveAsync(registrationId, loginResult.Session, cancellationToken);
        return new TryLoginResult(IsSuccess: true);
    }

    public async Task<IReadOnlyList<ForumTargetNode>> GetTargetHierarchyAsync(
        int registrationId,
        CancellationToken cancellationToken = default
    )
    {
        var (site, session) = await GetSiteAndValidSessionAsync(registrationId, cancellationToken);
        return await site.GetTargetHierarchyAsync(session, cancellationToken);
    }

    public async Task<IReadOnlyList<ExistingThread>> FindExistingThreadsAsync(
        int registrationId,
        ForumTargetId target,
        string releaseName,
        CancellationToken cancellationToken = default
    )
    {
        var (forum, session) = await GetForumSiteAndValidSessionAsync(
            registrationId,
            cancellationToken
        );

        return await forum.FindExistingThreadsAsync(
            session: session,
            target: target,
            releaseName: releaseName,
            cancellationToken: cancellationToken
        );
    }

    public async Task<IReadOnlyList<ExistingThread>> FindExistingThreadsAsync(
        int registrationId,
        string targetNodeId,
        string releaseName,
        CancellationToken cancellationToken = default
    )
    {
        var (forum, session) = await GetForumSiteAndValidSessionAsync(
            registrationId,
            cancellationToken
        );

        return await forum.FindExistingThreadsAsync(
            session: session,
            target: forum.CreateForumTargetIdFromStoredValue(session, targetNodeId),
            releaseName: releaseName,
            cancellationToken: cancellationToken
        );
    }

    public async Task<IReadOnlyList<ThreadPrefix>> GetThreadPrefixesAsync(
        int registrationId,
        ForumTargetId target,
        CancellationToken cancellationToken = default
    )
    {
        var (forum, session) = await GetForumSiteAndValidSessionAsync(
            registrationId,
            cancellationToken
        );
        return await forum.GetThreadPrefixesAsync(session, target, cancellationToken);
    }

    public async Task<PreparedDraft> PrepareNewThreadDraftAsync(
        int registrationId,
        ForumTargetId target,
        string title,
        IReadOnlyList<string> prefixIds,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        var (forum, session) = await GetForumSiteAndValidSessionAsync(
            registrationId,
            cancellationToken
        );

        return await forum.PrepareNewThreadDraftAsync(
            session: session,
            target: target,
            title: title,
            prefixIds: prefixIds,
            body: body,
            cancellationToken: cancellationToken
        );
    }

    public async Task<PreparedDraft> PrepareReplyDraftAsync(
        int registrationId,
        string threadUrl,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        var (forum, session) = await GetForumSiteAndValidSessionAsync(
            registrationId,
            cancellationToken
        );
        return await forum.PrepareReplyDraftAsync(session, threadUrl, body, cancellationToken);
    }

    public async Task<SubmittedPost> SubmitNewThreadAsync(
        int registrationId,
        string targetNodeId,
        string title,
        IReadOnlyList<string> prefixIds,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        var (forum, session) = await GetForumSiteAndValidSessionAsync(
            registrationId,
            cancellationToken
        );

        return await forum.SubmitNewThreadAsync(
            session: session,
            target: forum.CreateForumTargetIdFromStoredValue(session, targetNodeId),
            title: title,
            prefixIds: prefixIds,
            body: body,
            cancellationToken: cancellationToken
        );
    }

    public async Task<SubmittedPost> SubmitReplyAsync(
        int registrationId,
        string threadUrl,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        var (forum, session) = await GetForumSiteAndValidSessionAsync(
            registrationId,
            cancellationToken
        );
        return await forum.SubmitReplyAsync(session, threadUrl, body, cancellationToken);
    }

    public async Task<PreparedDraft> PrepareNewPostDraftAsync(
        int registrationId,
        ForumTargetId target,
        string title,
        string body,
        string? coverImageUrl,
        CancellationToken cancellationToken = default
    )
    {
        var (blog, config, session) = await GetBlogSiteConfigAndValidSessionAsync(
            registrationId,
            cancellationToken
        );

        return await blog.PrepareNewPostDraftAsync(
            config: config,
            session: session,
            target: target,
            title: title,
            body: body,
            coverImageUrl: coverImageUrl,
            cancellationToken: cancellationToken
        );
    }

    public async Task<SubmittedPost> SubmitNewPostAsync(
        int registrationId,
        string targetNodeId,
        string title,
        string body,
        string? coverImageUrl,
        CancellationToken cancellationToken = default
    )
    {
        var (blog, config, session) = await GetBlogSiteConfigAndValidSessionAsync(
            registrationId,
            cancellationToken
        );

        return await blog.SubmitNewPostAsync(
            config: config,
            session: session,
            target: blog.CreateForumTargetIdFromStoredValue(session, targetNodeId),
            title: title,
            body: body,
            coverImageUrl: coverImageUrl,
            cancellationToken: cancellationToken
        );
    }

    public async Task<IReadOnlyList<ExistingBlogPost>> FindExistingPostsAsync(
        int registrationId,
        string title,
        CancellationToken cancellationToken = default
    )
    {
        var (blog, _, session) = await GetBlogSiteConfigAndValidSessionAsync(
            registrationId,
            cancellationToken
        );

        return await blog.FindExistingPostsAsync(session, title, cancellationToken);
    }

    public async Task<SubmittedPost> PublishExistingPostAsync(
        int registrationId,
        string postedUrl,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        var (blog, _, session) = await GetBlogSiteConfigAndValidSessionAsync(
            registrationId,
            cancellationToken
        );

        return await blog.PublishExistingPostAsync(session, postedUrl, body, cancellationToken);
    }

    public async Task<SubmittedPost> EditPostAsync(
        int registrationId,
        string postedUrl,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        var (site, session) = await GetSiteAndValidSessionAsync(registrationId, cancellationToken);
        return await site.EditPostAsync(session, postedUrl, body, cancellationToken);
    }

    public async Task<string?> FindUrlOfSubmittedPostAsync(
        int registrationId,
        ForumTargetId target,
        bool isNewThread,
        string threadUrl,
        string title,
        CancellationToken cancellationToken = default
    )
    {
        var (forum, session) = await GetForumSiteAndValidSessionAsync(
            registrationId,
            cancellationToken
        );

        return await forum.FindUrlOfSubmittedPostAsync(
            session: session,
            target: target,
            isNewThread: isNewThread,
            threadUrl: threadUrl,
            title: title,
            cancellationToken: cancellationToken
        );
    }

    private async Task<(
        IForumDistributionSite Forum,
        DistributionSession Session
    )> GetForumSiteAndValidSessionAsync(int registrationId, CancellationToken cancellationToken)
    {
        var (site, session) = await GetSiteAndValidSessionAsync(registrationId, cancellationToken);

        if (site is not IForumDistributionSite forum)
        {
            throw new InvalidOperationException(
                $"Distribution site '{site.GetType().Name}' is not a forum."
            );
        }

        return (forum, session);
    }

    private async Task<(
        IBlogDistributionSite Blog,
        IDistributionSiteConfig Config,
        DistributionSession Session
    )> GetBlogSiteConfigAndValidSessionAsync(
        int registrationId,
        CancellationToken cancellationToken
    )
    {
        var (site, config, session) = await GetSiteConfigAndValidSessionAsync(
            registrationId,
            cancellationToken
        );

        if (site is not IBlogDistributionSite blog)
        {
            throw new InvalidOperationException(
                $"Distribution site '{site.GetType().Name}' is not a blog."
            );
        }

        return (blog, config, session);
    }

    private async Task<(
        IDistributionSite Site,
        DistributionSession Session
    )> GetSiteAndValidSessionAsync(int registrationId, CancellationToken cancellationToken)
    {
        var (site, _, session) = await GetSiteConfigAndValidSessionAsync(
            registrationId,
            cancellationToken
        );

        return (site, session);
    }

    private async Task<(
        IDistributionSite Site,
        IDistributionSiteConfig Config,
        DistributionSession Session
    )> GetSiteConfigAndValidSessionAsync(int registrationId, CancellationToken cancellationToken)
    {
        var registration = await repository.GetByIdAsync(registrationId, cancellationToken);
        var site = distributionSiteFactory.GetByClassName(registration.DistributionSiteClassName);
        var config = DeserializeConfig(site, registration);

        var session = await GetStoredValidSessionOrLogInAsync(
            registration: registration,
            site: site,
            config: config,
            cancellationToken: cancellationToken
        );

        return (site, config, session);
    }

    private async Task<DistributionSession> GetStoredValidSessionOrLogInAsync(
        DistributionSiteRegistration registration,
        IDistributionSite site,
        IDistributionSiteConfig config,
        CancellationToken cancellationToken
    )
    {
        var storedSession = await sessionRepository.GetByRegistrationIdAsync(
            registration.Id,
            cancellationToken
        );
        if (
            storedSession is not null
            && await site.IsSessionValidAsync(storedSession, cancellationToken)
        )
        {
            return storedSession;
        }

        var loginResult = await site.LogInAsync(config, cancellationToken);
        if (loginResult.Session is null)
        {
            throw new InvalidOperationException(
                $"Login to distribution site '{registration.Name}' failed: {loginResult.ErrorMessage}"
            );
        }

        await sessionRepository.SaveAsync(registration.Id, loginResult.Session, cancellationToken);
        return loginResult.Session;
    }

    private IDistributionSiteConfig DeserializeConfig(
        IDistributionSite site,
        DistributionSiteRegistration registration
    )
    {
        return site.DeserializeConfig(secretProtector.Unprotect(registration.SerializedConfig));
    }
}
