using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Bearcat.Abstractions.DistributionSite.Dto;

namespace Bearcat.DistributionSites.Shared.XenForo.Api;

public sealed partial class XenForoForumClient : IDisposable
{
    public const string HttpClientName = "XenForo";

    private readonly Uri baseUri;
    private readonly HttpClient http;
    private readonly HtmlParser parser = new();

    public XenForoForumClient(
        IHttpClientFactory httpClientFactory,
        Uri baseUri,
        DistributionSession session
    )
    {
        this.baseUri = baseUri;

        http = httpClientFactory.CreateClient(HttpClientName);
        http.BaseAddress = baseUri;
        http.DefaultRequestHeaders.UserAgent.ParseAdd(session.UserAgent);
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de-DE,de;q=0.9");
        http.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9"
        );
        http.DefaultRequestHeaders.TryAddWithoutValidation(
            "Cookie",
            string.Join("; ", session.Cookies.Select(cookie => $"{cookie.Name}={cookie.Value}"))
        );
    }

    public async Task<bool> IsLoggedInAsync(CancellationToken cancellationToken)
    {
        var document = await GetDocumentAsync("/", cancellationToken);
        var loggedIn = document.QuerySelector("html")?.GetAttribute("data-logged-in");

        return string.Equals(loggedIn, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<ForumTargetNode>> GetForumTreeAsync(
        CancellationToken cancellationToken
    )
    {
        var document = await GetDocumentAsync("/", cancellationToken);

        var roots = new List<NodeBuilder>();
        NodeBuilder? currentCategory = null;

        foreach (var card in document.QuerySelectorAll(".node--category, .node--forum"))
        {
            if (card.ClassList.Contains("node--category"))
            {
                currentCategory = new NodeBuilder(
                    BuildNodeId(card),
                    BuildNodeTitle(card),
                    canReceivePosts: false
                );
                roots.Add(currentCategory);
                continue;
            }

            var forum = new NodeBuilder(
                BuildNodeId(card),
                BuildNodeTitle(card),
                canReceivePosts: true
            );

            foreach (var subforum in ReadSubforums(card))
            {
                forum.Children.Add(subforum);
            }

            if (currentCategory is null)
            {
                roots.Add(forum);
            }
            else
            {
                currentCategory.Children.Add(forum);
            }
        }

        return roots.Select(builder => builder.Build()).ToList();
    }

    public async Task<IReadOnlyList<ExistingThread>> SearchThreadsAsync(
        string keywords,
        string? forumUrl,
        CancellationToken cancellationToken
    )
    {
        var token = ExtractToken(await GetDocumentAsync("/search/", cancellationToken));

        var form = new List<KeyValuePair<string, string>>
        {
            new("keywords", keywords),
            new("c[title_only]", "1"),
            new("order", "relevance"),
            new("grouped", "1"),
            new("_xfToken", token),
        };

        if (ExtractNodeId(forumUrl) is { } nodeId)
        {
            form.Add(new KeyValuePair<string, string>("c[nodes][0]", nodeId.ToString()));
        }

        using var response = await http.PostAsync(
            requestUri: "/search/search",
            content: new FormUrlEncodedContent(form),
            cancellationToken: cancellationToken
        );

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = await parser.ParseDocumentAsync(html, cancellationToken);

        var results = new List<ExistingThread>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in document.QuerySelectorAll(".contentRow-title a"))
        {
            var href = link.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var absolute = new Uri(baseUri, href).ToString();

            if (seen.Add(absolute))
            {
                results.Add(new ExistingThread(link.TextContent.Trim(), absolute));
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<ThreadPrefix>> GetThreadPrefixesAsync(
        string forumUrl,
        CancellationToken cancellationToken
    )
    {
        var formUrl = new Uri(EnsureTrailingSlash(forumUrl), "post-thread");
        var document = await GetDocumentAsync(formUrl.ToString(), cancellationToken);

        var select =
            document.QuerySelector("select[name='prefix_id[]']")
            ?? document.QuerySelector("select[name='prefix_id']");

        if (select is null)
        {
            return [];
        }

        return select
            .QuerySelectorAll("option")
            .Select(option => new ThreadPrefix(
                option.GetAttribute("value") ?? string.Empty,
                option.TextContent.Trim()
            ))
            .Where(prefix => prefix.Id is not ("" or "0"))
            .ToList();
    }

    public async Task<PreparedDraft> PrepareNewThreadDraftAsync(
        string forumUrl,
        string title,
        IReadOnlyList<string> prefixIds,
        string body,
        CancellationToken cancellationToken
    )
    {
        var formUrl = new Uri(EnsureTrailingSlash(forumUrl), "post-thread");
        var (token, draftUrl) = await GetPageContextAsync(formUrl, cancellationToken);

        var fields = new List<KeyValuePair<string, string>>
        {
            new("title", title),
            new("message", body),
        };
        fields.AddRange(
            prefixIds.Select(prefixId => new KeyValuePair<string, string>("prefix_id[]", prefixId))
        );

        await SaveDraftAsync(
            draftUrl: draftUrl,
            fields: fields,
            token: token,
            cancellationToken: cancellationToken
        );

        return new PreparedDraft(formUrl.ToString(), RequiresSameAccountBrowserSession: true);
    }

    public async Task<PreparedDraft> PrepareReplyDraftAsync(
        string threadUrl,
        string body,
        CancellationToken cancellationToken
    )
    {
        var thread = EnsureTrailingSlash(threadUrl);
        var (token, draftUrl) = await GetPageContextAsync(thread, cancellationToken);

        await SaveDraftAsync(
            draftUrl: draftUrl,
            fields: new Dictionary<string, string> { ["message"] = body },
            token: token,
            cancellationToken: cancellationToken
        );

        return new PreparedDraft(thread.ToString(), RequiresSameAccountBrowserSession: true);
    }

    public async Task<string?> GetLoggedInUsernameAsync(CancellationToken cancellationToken)
    {
        var document = await GetDocumentAsync("/", cancellationToken);
        var username = document
            .QuerySelector(".p-navgroup-link--user .p-navgroup-linkText")
            ?.TextContent.Trim();

        return string.IsNullOrWhiteSpace(username) ? null : username;
    }

    public async Task<string?> FindLatestPostUrlInThreadAsync(
        string threadUrl,
        string username,
        CancellationToken cancellationToken
    )
    {
        var document = await GetDocumentAsync(threadUrl, cancellationToken);

        var lastPage = GetLastPageNumber(document);
        if (lastPage > 1)
        {
            var pageUrl = new Uri(EnsureTrailingSlash(threadUrl), $"page-{lastPage}");
            document = await GetDocumentAsync(pageUrl.ToString(), cancellationToken);
        }

        return BuildPostUrl(FindUserPostId(document, username, takeLast: true));
    }

    public async Task<string?> FindNewThreadPostUrlAsync(
        string? forumUrl,
        string title,
        string username,
        CancellationToken cancellationToken
    )
    {
        var threads = await SearchThreadsAsync(
            keywords: title,
            forumUrl: forumUrl,
            cancellationToken: cancellationToken
        );

        foreach (var thread in threads)
        {
            var document = await GetDocumentAsync(thread.Url, cancellationToken);
            var postId = FindUserPostId(document, username, takeLast: false);
            if (postId is not null)
            {
                return BuildPostUrl(postId);
            }
        }

        return null;
    }

    private string? BuildPostUrl(string? postId)
    {
        return postId is null ? null : new Uri(baseUri, $"/posts/{postId}/").ToString();
    }

    private static string? FindUserPostId(IDocument document, string username, bool takeLast)
    {
        var posts = document
            .QuerySelectorAll("article.message--post")
            .Where(post =>
                string.Equals(
                    post.GetAttribute("data-author"),
                    username,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .ToList();

        var post = takeLast ? posts.LastOrDefault() : posts.FirstOrDefault();
        var content = post?.GetAttribute("data-content");
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var match = PostIdPattern().Match(content);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static int GetLastPageNumber(IDocument document)
    {
        return document
            .QuerySelectorAll(".pageNav-main .pageNav-page a")
            .Select(link => int.TryParse(link.TextContent.Trim(), out var page) ? page : 1)
            .DefaultIfEmpty(1)
            .Max();
    }

    private List<NodeBuilder> ReadSubforums(IElement card)
    {
        var subforums = new List<NodeBuilder>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (
            var link in card.QuerySelectorAll(
                ".node-subNodesFlat a, .node-subForums a, .subNodeFlatList a, .node-extra a"
            )
        )
        {
            var href = link.GetAttribute("href");
            if (
                string.IsNullOrWhiteSpace(href)
                || !href.Contains("/forum", StringComparison.Ordinal)
            )
            {
                continue;
            }

            var absolute = new Uri(baseUri, href).ToString();
            if (seen.Add(absolute))
            {
                subforums.Add(
                    new NodeBuilder(
                        new ForumTargetId(absolute),
                        link.TextContent.Trim(),
                        canReceivePosts: true
                    )
                );
            }
        }

        return subforums;
    }

    private ForumTargetId BuildNodeId(IElement card)
    {
        var titleLink = card.QuerySelector(".node-title a");
        var href = titleLink?.GetAttribute("href");
        if (!string.IsNullOrWhiteSpace(href))
        {
            return new ForumTargetId(new Uri(baseUri, href).ToString());
        }

        return new ForumTargetId(
            card.Id ?? card.QuerySelector(".node-title")?.TextContent.Trim() ?? string.Empty
        );
    }

    private static string BuildNodeTitle(IElement card)
    {
        var titleLink = card.QuerySelector(".node-title a") ?? card.QuerySelector(".node-title");
        return titleLink?.TextContent.Trim() ?? string.Empty;
    }

    private async Task SaveDraftAsync(
        Uri draftUrl,
        IEnumerable<KeyValuePair<string, string>> fields,
        string token,
        CancellationToken cancellationToken
    )
    {
        var form = new List<KeyValuePair<string, string>>(fields)
        {
            new("_xfToken", token),
            new("_xfResponseType", "json"),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, draftUrl);
        request.Content = new FormUrlEncodedContent(form);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (
            !body.Contains("\"status\": \"ok\"", StringComparison.Ordinal)
            && !body.Contains("\"status\":\"ok\"", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                $"Draft save did not return ok: {body[..Math.Min(body.Length, 200)]}"
            );
        }
    }

    private async Task<(string Token, Uri DraftUrl)> GetPageContextAsync(
        Uri pageUrl,
        CancellationToken cancellationToken
    )
    {
        var document = await GetDocumentAsync(pageUrl.ToString(), cancellationToken);
        var token = ExtractToken(document);

        var draftHref = document.QuerySelector("[data-draft-url]")?.GetAttribute("data-draft-url");
        var draftUrl = string.IsNullOrWhiteSpace(draftHref)
            ? new Uri(EnsureTrailingSlash(pageUrl.ToString()), "draft")
            : new Uri(baseUri, draftHref);

        return (token, draftUrl);
    }

    private async Task<IHtmlDocument> GetDocumentAsync(
        string url,
        CancellationToken cancellationToken
    )
    {
        var html = await http.GetStringAsync(url, cancellationToken);
        return await parser.ParseDocumentAsync(html, cancellationToken);
    }

    private static string ExtractToken(IDocument document)
    {
        var token =
            document.QuerySelector("html")?.GetAttribute("data-csrf")
            ?? document.QuerySelector("input[name='_xfToken']")?.GetAttribute("value");

        return string.IsNullOrWhiteSpace(token)
            ? throw new InvalidOperationException("Could not locate the XenForo CSRF token.")
            : token;
    }

    private static int? ExtractNodeId(string? forumUrl)
    {
        if (string.IsNullOrWhiteSpace(forumUrl))
        {
            return null;
        }

        var match = NodeIdPattern().Match(forumUrl.TrimEnd('/'));
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private static Uri EnsureTrailingSlash(string url)
    {
        return new Uri(url.EndsWith('/') ? url : url + "/");
    }

    public void Dispose()
    {
        http.Dispose();
    }

    [GeneratedRegex(@"(\d+)$")]
    private static partial Regex NodeIdPattern();

    [GeneratedRegex(@"post-(\d+)")]
    private static partial Regex PostIdPattern();

    private sealed class NodeBuilder(ForumTargetId id, string title, bool canReceivePosts)
    {
        public List<NodeBuilder> Children { get; } = [];

        public ForumTargetNode Build()
        {
            return new ForumTargetNode(
                Id: id,
                Title: title,
                CanReceivePosts: canReceivePosts,
                Children: Children.Select(child => child.Build()).ToList()
            );
        }
    }
}
