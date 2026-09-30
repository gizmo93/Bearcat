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

        var loginResult = await LogInAsync(site, registration, cancellationToken);
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
        var (forum, session) = await GetForumSiteAndValidSessionAsync(
            registrationId,
            cancellationToken
        );
        return await forum.GetTargetHierarchyAsync(session, cancellationToken);
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

    public async Task<SubmittedPost> EditPostAsync(
        int registrationId,
        string postedUrl,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        var (forum, session) = await GetForumSiteAndValidSessionAsync(
            registrationId,
            cancellationToken
        );
        return await forum.EditPostAsync(session, postedUrl, body, cancellationToken);
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
        var registration = await repository.GetByIdAsync(registrationId, cancellationToken);
        var site = distributionSiteFactory.GetByClassName(registration.DistributionSiteClassName);

        if (site is not IForumDistributionSite forum)
        {
            throw new InvalidOperationException(
                $"Distribution site '{registration.DistributionSiteClassName}' is not a forum."
            );
        }

        var session = await GetStoredValidSessionOrLogInAsync(
            registration,
            site,
            cancellationToken
        );
        return (forum, session);
    }

    private async Task<DistributionSession> GetStoredValidSessionOrLogInAsync(
        DistributionSiteRegistration registration,
        IDistributionSite site,
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

        var loginResult = await LogInAsync(site, registration, cancellationToken);
        if (loginResult.Session is null)
        {
            throw new InvalidOperationException(
                $"Login to distribution site '{registration.Name}' failed: {loginResult.ErrorMessage}"
            );
        }

        await sessionRepository.SaveAsync(registration.Id, loginResult.Session, cancellationToken);
        return loginResult.Session;
    }

    private Task<DistributionSiteLoginResult> LogInAsync(
        IDistributionSite site,
        DistributionSiteRegistration registration,
        CancellationToken cancellationToken
    )
    {
        var config = site.DeserializeConfig(
            secretProtector.Unprotect(registration.SerializedConfig)
        );

        return site.LogInAsync(config, cancellationToken);
    }
}
