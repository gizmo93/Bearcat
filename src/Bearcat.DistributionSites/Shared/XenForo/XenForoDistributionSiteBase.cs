using System.Text.Json;
using Bearcat.Abstractions.ConfigurationFields;
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

    public virtual PostContentFormat ContentFormat => PostContentFormat.BBCode;

    public virtual IReadOnlyList<ConfigurationField> ConfigurationFields =>
        [
            new(
                nameof(IXenForoDistributionSiteConfig.Username),
                ConfigurationFieldType.Text,
                IsRequired: true
            ),
            new(
                nameof(IXenForoDistributionSiteConfig.Password),
                ConfigurationFieldType.Password,
                IsRequired: true
            ),
        ];

    public abstract string GetBaseUrl(IDistributionSiteConfig config);

    public IDistributionSiteConfig DeserializeConfig(string serializedConfig)
    {
        return JsonSerializer.Deserialize<TConfig>(
                serializedConfig,
                XenForoConfigSerializerOptions.CaseInsensitivePropertyNames
            )
            ?? throw new InvalidOperationException(
                $"Could not deserialize the {typeof(TConfig).Name} configuration."
            );
    }

    public Task<DistributionSession?> LogInAsync(
        IDistributionSiteConfig config,
        CancellationToken cancellationToken
    )
    {
        var xenForoConfig = config.As<TConfig>();
        return XenForoBrowserLogin.LoginAsync(
            baseUrl: GetBaseUrl(config),
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

    public async Task<SubmittedPost> SubmitNewThreadAsync(
        DistributionSession session,
        ForumTargetId target,
        string title,
        IReadOnlyList<string> prefixIds,
        string body,
        CancellationToken cancellationToken
    )
    {
        using var client = CreateClient(session);
        return await client.SubmitNewThreadAsync(
            forumUrl: target.Value,
            title: title,
            prefixIds: prefixIds,
            message: body,
            cancellationToken: cancellationToken
        );
    }

    public async Task<SubmittedPost> SubmitReplyAsync(
        DistributionSession session,
        string threadUrl,
        string body,
        CancellationToken cancellationToken
    )
    {
        using var client = CreateClient(session);
        return await client.SubmitReplyAsync(threadUrl, body, cancellationToken);
    }

    public async Task<SubmittedPost> EditPostAsync(
        DistributionSession session,
        string postedUrl,
        string body,
        CancellationToken cancellationToken
    )
    {
        using var client = CreateClient(session);
        return await client.EditPostAsync(postedUrl, body, cancellationToken);
    }

    public ForumTargetId CreateForumTargetIdFromStoredValue(
        DistributionSession session,
        string storedTargetId
    )
    {
        var trimmed = storedTargetId.Trim();

        return trimmed.Length > 0 && trimmed.All(char.IsAsciiDigit)
            ? new ForumTargetId($"{session.BaseUrl.TrimEnd('/')}/forums/{trimmed}/")
            : new ForumTargetId(trimmed);
    }

    public async Task<string?> FindUrlOfSubmittedPostAsync(
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
        var baseUri = new Uri(
            session.BaseUrl.EndsWith('/') ? session.BaseUrl : session.BaseUrl + "/"
        );
        return new XenForoForumClient(httpClientFactory, baseUri, session);
    }
}
