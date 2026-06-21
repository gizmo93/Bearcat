using System.Text.Json;
using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.DistributionSites.Extensions;
using Bearcat.DistributionSites.Shared.XenForo.Api;

namespace Bearcat.DistributionSites.Shared.XenForo;

public abstract class XenForoDistributionSiteBase<TConfig>(IHttpClientFactory httpClientFactory)
    : IForumDistributionSite
    where TConfig : IXenForoDistributionSiteConfig
{
    public abstract string Name { get; }

    public abstract string BaseUrl { get; }

    public virtual PostContentFormat ContentFormat => PostContentFormat.BBCode;

    public IReadOnlyList<string> ConfigurationKeys =>
        [
            nameof(IXenForoDistributionSiteConfig.Username),
            nameof(IXenForoDistributionSiteConfig.Password),
        ];

    public IDistributionSiteConfig DeserializeConfig(string serializedConfig)
    {
        return JsonSerializer.Deserialize<TConfig>(serializedConfig)
            ?? throw new InvalidOperationException(
                $"Could not deserialize the {typeof(TConfig).Name} configuration."
            );
    }

    public string SerializeConfig(Dictionary<string, string> config) =>
        JsonSerializer.Serialize(config);

    public Task<DistributionSession?> LogInAsync(
        IDistributionSiteConfig config,
        CancellationToken cancellationToken
    )
    {
        var xenForoConfig = config.As<TConfig>();
        return XenForoBrowserLogin.LoginAsync(
            baseUrl: BaseUrl,
            username: xenForoConfig.Username,
            password: xenForoConfig.Password
        );
    }

    public async Task<bool> IsSessionValidAsync(
        DistributionSession session,
        CancellationToken cancellationToken
    )
    {
        using var client = CreateClient(session);
        return await client.IsLoggedInAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ForumTargetNode>> GetTargetHierarchyAsync(
        DistributionSession session,
        CancellationToken cancellationToken
    )
    {
        using var client = CreateClient(session);
        return await client.GetForumTreeAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExistingThread>> FindExistingThreadsAsync(
        DistributionSession session,
        ForumTargetId target,
        string releaseName,
        CancellationToken cancellationToken
    )
    {
        using var client = CreateClient(session);
        return await client.SearchThreadsAsync(
            keywords: releaseName,
            forumUrl: target.Value,
            cancellationToken: cancellationToken
        );
    }

    public async Task<IReadOnlyList<ThreadPrefix>> GetThreadPrefixesAsync(
        DistributionSession session,
        ForumTargetId target,
        CancellationToken cancellationToken
    )
    {
        using var client = CreateClient(session);
        return await client.GetThreadPrefixesAsync(target.Value, cancellationToken);
    }

    public async Task<PreparedDraft> PrepareNewThreadDraftAsync(
        DistributionSession session,
        ForumTargetId target,
        string title,
        IReadOnlyList<string> prefixIds,
        string body,
        CancellationToken cancellationToken
    )
    {
        using var client = CreateClient(session);
        return await client.PrepareNewThreadDraftAsync(
            forumUrl: target.Value,
            title: title,
            prefixIds: prefixIds,
            body: body,
            cancellationToken: cancellationToken
        );
    }

    public async Task<PreparedDraft> PrepareReplyDraftAsync(
        DistributionSession session,
        string threadUrl,
        string body,
        CancellationToken cancellationToken
    )
    {
        using var client = CreateClient(session);
        return await client.PrepareReplyDraftAsync(threadUrl, body, cancellationToken);
    }

    public async Task<string?> ResolvePostedUrlAsync(
        DistributionSession session,
        ForumTargetId target,
        bool isNewThread,
        string threadUrl,
        string title,
        CancellationToken cancellationToken
    )
    {
        using var client = CreateClient(session);

        var username = await client.GetLoggedInUsernameAsync(cancellationToken);
        if (username is null)
        {
            return null;
        }

        return isNewThread
            ? await client.FindNewThreadPostUrlAsync(
                forumUrl: target.Value,
                title: title,
                username: username,
                cancellationToken: cancellationToken
            )
            : await client.FindLatestPostUrlInThreadAsync(
                threadUrl: threadUrl,
                username: username,
                cancellationToken: cancellationToken
            );
    }

    private XenForoForumClient CreateClient(DistributionSession session)
    {
        var baseUri = new Uri(BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/");
        return new XenForoForumClient(httpClientFactory, baseUri, session);
    }
}
