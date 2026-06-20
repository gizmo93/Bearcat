using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Abstractions.DistributionSite.Results;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;

namespace Bearcat.Domain.UseCases.ManageDistributionSites;

public class DistributionSiteSessionService(
    IDistributionSiteRegistrationWriteRepository repository,
    IDistributionSessionStore sessionStore,
    IDistributionSiteFactory distributionSiteFactory,
    ISecretProtector secretProtector
)
{
    public async Task<TryLoginResult> TestLoginAsync(
        int registrationId,
        CancellationToken cancellationToken = default
    )
    {
        var registration = await repository.GetByIdAsync(registrationId, cancellationToken);
        var site = distributionSiteFactory.Get(registration.DistributionSiteClassName);

        var session = await LogInAsync(site, registration, cancellationToken);
        if (session is null)
        {
            return new TryLoginResult(
                IsSuccess: false,
                ErrorMessage: "Login failed (credentials rejected or a challenge appeared)."
            );
        }

        await sessionStore.SaveAsync(registrationId, session, cancellationToken);
        return new TryLoginResult(IsSuccess: true);
    }

    public async Task<IReadOnlyList<ForumTargetNode>> GetTargetHierarchyAsync(
        int registrationId,
        CancellationToken cancellationToken = default
    )
    {
        var (forum, session) = await EnsureForumSessionAsync(registrationId, cancellationToken);
        return await forum.GetTargetHierarchyAsync(session, cancellationToken);
    }

    public async Task<IReadOnlyList<ExistingThread>> FindExistingThreadsAsync(
        int registrationId,
        ForumTargetId target,
        string releaseName,
        CancellationToken cancellationToken = default
    )
    {
        var (forum, session) = await EnsureForumSessionAsync(registrationId, cancellationToken);

        return await forum.FindExistingThreadsAsync(
            session: session,
            target: target,
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
        var (forum, session) = await EnsureForumSessionAsync(registrationId, cancellationToken);
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
        var (forum, session) = await EnsureForumSessionAsync(registrationId, cancellationToken);

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
        var (forum, session) = await EnsureForumSessionAsync(registrationId, cancellationToken);
        return await forum.PrepareReplyDraftAsync(session, threadUrl, body, cancellationToken);
    }

    private async Task<(
        IForumDistributionSite Forum,
        DistributionSession Session
    )> EnsureForumSessionAsync(int registrationId, CancellationToken cancellationToken)
    {
        var registration = await repository.GetByIdAsync(registrationId, cancellationToken);
        var site = distributionSiteFactory.Get(registration.DistributionSiteClassName);

        if (site is not IForumDistributionSite forum)
        {
            throw new InvalidOperationException(
                $"Distribution site '{registration.DistributionSiteClassName}' is not a forum."
            );
        }

        var session = await EnsureSessionAsync(registration, site, cancellationToken);
        return (forum, session);
    }

    private async Task<DistributionSession> EnsureSessionAsync(
        DistributionSiteRegistration registration,
        IDistributionSite site,
        CancellationToken cancellationToken
    )
    {
        var cached = await sessionStore.TryGetAsync(registration.Id, cancellationToken);
        if (cached is not null && await site.IsSessionValidAsync(cached, cancellationToken))
        {
            return cached;
        }

        var session =
            await LogInAsync(site, registration, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Login to distribution site '{registration.Name}' failed; could not establish a session."
            );

        await sessionStore.SaveAsync(registration.Id, session, cancellationToken);
        return session;
    }

    private Task<DistributionSession?> LogInAsync(
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
