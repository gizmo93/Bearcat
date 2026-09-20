using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
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
    private readonly CookieContainer cookies = new();

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

        foreach (var cookie in session.Cookies)
        {
            cookies.Add(
                new Cookie(
                    name: cookie.Name,
                    value: cookie.Value,
                    path: cookie.Path,
                    domain: cookie.Domain
                )
            );
        }
    }

    public async Task<bool> IsLoggedInAsync(CancellationToken cancellationToken)
    {
        var document = await GetDocumentAsync("", cancellationToken);
        var loggedIn = document.QuerySelector("html")?.GetAttribute("data-logged-in");

        return string.Equals(loggedIn, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<ForumTargetNode>> GetForumTreeAsync(
        CancellationToken cancellationToken
    )
    {
        var document = await GetDocumentAsync("", cancellationToken);

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
        var token = ExtractToken(await GetDocumentAsync("search/", cancellationToken));

        var form = new List<KeyValuePair<string, string>>
        {
            new("keywords", keywords),
            new("search_type", "post"),
            new("c[title_only]", "1"),
            new("order", "relevance"),
            new("grouped", "1"),
            new("_xfToken", token),
        };

        if (ExtractNodeId(forumUrl) is { } nodeId)
        {
            form.Add(new KeyValuePair<string, string>("c[nodes][0]", nodeId.ToString()));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(baseUri, "search/search")
        )
        {
            Content = new FormUrlEncodedContent(form),
        };
        using var response = await SendAsync(request, cancellationToken);

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

        return results
            .Select(thread => new { Thread = thread, ThreadId = ExtractThreadId(thread.Url) })
            .OrderBy(entry => entry.ThreadId is null)
            .ThenBy(entry => entry.ThreadId ?? 0)
            .Select(entry => entry.Thread)
            .ToList();
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

    public async Task<SubmittedPost> SubmitNewThreadAsync(
        string forumUrl,
        string title,
        IReadOnlyList<string> prefixIds,
        string message,
        CancellationToken cancellationToken
    )
    {
        var overrides = new List<KeyValuePair<string, string>>
        {
            new("title", title),
            new("message", message),
        };
        overrides.AddRange(
            prefixIds.Select(prefixId => new KeyValuePair<string, string>("prefix_id[]", prefixId))
        );

        return await SubmitFormAsync(
            pageUrl: new Uri(EnsureTrailingSlash(forumUrl), "post-thread"),
            actionMarker: "post-thread",
            overrides: overrides,
            cancellationToken: cancellationToken
        );
    }

    public async Task<SubmittedPost> SubmitReplyAsync(
        string threadUrl,
        string message,
        CancellationToken cancellationToken
    )
    {
        return await SubmitFormAsync(
            pageUrl: EnsureTrailingSlash(threadUrl),
            actionMarker: "add-reply",
            overrides: [new KeyValuePair<string, string>("message", message)],
            cancellationToken: cancellationToken
        );
    }

    public async Task<SubmittedPost> EditPostAsync(
        string postedUrl,
        string message,
        CancellationToken cancellationToken
    )
    {
        var postId = await ResolvePostIdAsync(postedUrl, cancellationToken);
        var editPath = $"posts/{postId}/edit";

        return await SubmitFormAsync(
            pageUrl: new Uri(baseUri, editPath),
            actionMarker: editPath,
            overrides: [new KeyValuePair<string, string>("message", message)],
            cancellationToken: cancellationToken,
            keepExistingFieldValues: true
        );
    }

    private async Task<string> ResolvePostIdAsync(
        string postedUrl,
        CancellationToken cancellationToken
    )
    {
        if (ExtractPostId(postedUrl) is { } knownPostId)
        {
            return knownPostId;
        }

        var username =
            await GetLoggedInUsernameAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Could not determine the logged in XenForo user."
            );

        var document = await GetDocumentAsync(postedUrl, cancellationToken);

        var permalink =
            ExtractPostPermalink(FindUserPost(document, username, takeLast: false))
            ?? throw new InvalidOperationException(
                $"Could not locate a post of '{username}' at {postedUrl}."
            );

        return ExtractPostId(permalink)
            ?? throw new InvalidOperationException(
                $"Could not determine the post id of {permalink}."
            );
    }

    public async Task<string?> GetLoggedInUsernameAsync(CancellationToken cancellationToken)
    {
        var document = await GetDocumentAsync("", cancellationToken);
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

        return ExtractPostPermalink(FindUserPost(document, username, takeLast: true));
    }

    public async Task<string?> FindNewThreadPostUrlAsync(
        string? forumUrl,
        string title,
        string username,
        CancellationToken cancellationToken
    )
    {
        var threadUrl = await FindNewThreadUrlAsync(
            forumUrl: forumUrl,
            title: title,
            username: username,
            cancellationToken: cancellationToken
        );

        if (threadUrl is null)
        {
            return null;
        }

        var document = await GetDocumentAsync(threadUrl, cancellationToken);
        return ExtractPostPermalink(FindUserPost(document, username, takeLast: false));
    }

    private async Task<string?> FindNewThreadUrlAsync(
        string? forumUrl,
        string title,
        string username,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(forumUrl))
        {
            var listingUrl = forumUrl.TrimEnd('/') + "/?order=post_date&direction=desc";
            var listing = await GetDocumentAsync(listingUrl, cancellationToken);
            var listedHref = FindThreadHrefInListing(listing, title, username);
            if (listedHref is not null)
            {
                return new Uri(baseUri, listedHref).ToString();
            }
        }

        var threads = await SearchThreadsAsync(
            keywords: title,
            forumUrl: forumUrl,
            cancellationToken: cancellationToken
        );

        ExistingThread? matchedThread = null;
        foreach (var thread in threads)
        {
            var document = await GetDocumentAsync(thread.Url, cancellationToken);
            if (FindUserPost(document, username, takeLast: false) is not null)
            {
                matchedThread = thread;
                break;
            }
        }

        return matchedThread?.Url;
    }

    private static string? FindThreadHrefInListing(
        IDocument document,
        string title,
        string username
    )
    {
        var normalizedTitle = NormalizeForMatch(title);

        var rows = document
            .QuerySelectorAll(".structItem--thread")
            .Where(row =>
                string.Equals(
                    row.GetAttribute("data-author"),
                    username,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .ToList();

        var matched =
            rows.FirstOrDefault(row =>
                NormalizeForMatch(
                        row.QuerySelector(".structItem-title")?.TextContent ?? string.Empty
                    )
                    .Contains(normalizedTitle, StringComparison.Ordinal)
            ) ?? rows.FirstOrDefault();

        return matched
            ?.QuerySelectorAll(".structItem-title a")
            .LastOrDefault()
            ?.GetAttribute("href");
    }

    private string? ExtractPostPermalink(IElement? post)
    {
        if (post is null)
        {
            return null;
        }

        var href =
            post.QuerySelector(".message-attribution-main a")?.GetAttribute("href")
            ?? post.QuerySelectorAll("a")
                .Select(anchor => anchor.GetAttribute("href"))
                .FirstOrDefault(value =>
                    !string.IsNullOrWhiteSpace(value)
                    && value.Contains("post-", StringComparison.Ordinal)
                );

        return string.IsNullOrWhiteSpace(href) ? null : new Uri(baseUri, href).ToString();
    }

    private static IElement? FindUserPost(IDocument document, string username, bool takeLast)
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

        return takeLast ? posts.LastOrDefault() : posts.FirstOrDefault();
    }

    private static string NormalizeForMatch(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        return builder.ToString().Trim();
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
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var uri = new Uri(baseUri, href);
            if (!uri.AbsolutePath.Contains("/forums/", StringComparison.Ordinal))
            {
                continue;
            }

            var absolute = uri.ToString();
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
        IReadOnlyCollection<KeyValuePair<string, string>> fields,
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

        using var response = await SendAsync(request, cancellationToken);
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

    private async Task<SubmittedPost> SubmitFormAsync(
        Uri pageUrl,
        string actionMarker,
        List<KeyValuePair<string, string>> overrides,
        CancellationToken cancellationToken,
        bool keepExistingFieldValues = false
    )
    {
        var document = await GetDocumentAsync(pageUrl.ToString(), cancellationToken);

        var form =
            FindForm(document, actionMarker)
            ?? throw new InvalidOperationException(
                $"Could not locate the '{actionMarker}' form at {pageUrl}."
            );

        var fields = keepExistingFieldValues
            ? CollectFormFields(form, overrides)
            : CollectHiddenFields(form, overrides);

        fields.AddRange(overrides);

        if (fields.TrueForAll(field => field.Key != "_xfToken"))
        {
            fields.Add(new KeyValuePair<string, string>("_xfToken", ExtractToken(document)));
        }

        fields.Add(new KeyValuePair<string, string>("_xfResponseType", "json"));

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            ResolveFormAction(form, pageUrl)
        )
        {
            Content = new FormUrlEncodedContent(fields),
        };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        using var response = await SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new SubmittedPost(ExtractRedirectUrl(body));
    }

    private static IElement? FindForm(IDocument document, string actionMarker)
    {
        return document
            .QuerySelectorAll("form")
            .FirstOrDefault(form =>
                form.GetAttribute("action")?.Contains(actionMarker, StringComparison.Ordinal)
                == true
            );
    }

    private static List<KeyValuePair<string, string>> CollectFormFields(
        IElement form,
        List<KeyValuePair<string, string>> overrides
    )
    {
        var overridden = BuildOverriddenNames(overrides);
        var fields = new List<KeyValuePair<string, string>>();

        foreach (var control in form.QuerySelectorAll("input, select, textarea"))
        {
            var name = control.GetAttribute("name");

            if (string.IsNullOrEmpty(name) || overridden.Contains(NormalizeFieldName(name)))
            {
                continue;
            }

            fields.AddRange(
                ReadControlValues(control)
                    .Select(value => new KeyValuePair<string, string>(name, value))
            );
        }

        return fields;
    }

    private static IReadOnlyList<string> ReadControlValues(IElement control)
    {
        return control switch
        {
            IHtmlSelectElement select => select
                .SelectedOptions.Select(option => option.Value)
                .ToList(),
            IHtmlTextAreaElement textArea => [textArea.Value],
            IHtmlInputElement input => ReadInputValues(input),
            _ => [],
        };
    }

    private static IReadOnlyList<string> ReadInputValues(IHtmlInputElement input)
    {
        if (input.Type is "submit" or "reset" or "button" or "image" or "file")
        {
            return [];
        }

        return input.Type is "checkbox" or "radio" && !input.IsChecked ? [] : [input.Value];
    }

    private static List<KeyValuePair<string, string>> CollectHiddenFields(
        IElement form,
        List<KeyValuePair<string, string>> overrides
    )
    {
        var overridden = BuildOverriddenNames(overrides);

        return form.QuerySelectorAll("input[type='hidden']")
            .Select(input => new KeyValuePair<string, string>(
                input.GetAttribute("name") ?? string.Empty,
                input.GetAttribute("value") ?? string.Empty
            ))
            .Where(field =>
                field.Key.Length > 0 && !overridden.Contains(NormalizeFieldName(field.Key))
            )
            .ToList();
    }

    private static HashSet<string> BuildOverriddenNames(
        List<KeyValuePair<string, string>> overrides
    )
    {
        return overrides
            .Select(field => NormalizeFieldName(field.Key))
            .Append("_xfResponseType")
            .ToHashSet(StringComparer.Ordinal);
    }

    private Uri ResolveFormAction(IElement form, Uri pageUrl)
    {
        var action = form.GetAttribute("action");
        return string.IsNullOrWhiteSpace(action) ? pageUrl : new Uri(baseUri, action);
    }

    private static string NormalizeFieldName(string name)
    {
        return name.EndsWith("[]", StringComparison.Ordinal) ? name[..^2] : name;
    }

    private string ExtractRedirectUrl(string body)
    {
        using var json = ParseJson(body);
        var root = json.RootElement;

        if (root.ValueKind is not JsonValueKind.Object)
        {
            throw new InvalidOperationException(UnexpectedResponseMessage(body));
        }

        if (FlattenErrors(root) is { Count: > 0 } errors)
        {
            throw new InvalidOperationException(
                $"XenForo rejected the post: {string.Join(" | ", errors)}"
            );
        }

        var status = ReadString(root, "status");
        var redirect = ReadString(root, "redirect");

        if (
            !string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(redirect)
        )
        {
            throw new InvalidOperationException(UnexpectedResponseMessage(body));
        }

        return new Uri(baseUri, redirect).ToString();
    }

    private static JsonDocument ParseJson(string body)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(UnexpectedResponseMessage(body), exception);
        }
    }

    private List<string> FlattenErrors(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errors))
        {
            return [];
        }

        IEnumerable<string?> texts = errors.ValueKind switch
        {
            JsonValueKind.Array => errors.EnumerateArray().Select(ReadErrorText),
            JsonValueKind.Object => errors
                .EnumerateObject()
                .Select(property => ReadErrorText(property.Value)),
            JsonValueKind.String => [errors.GetString()],
            _ => [],
        };

        return texts
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => StripHtml(text!))
            .Where(text => text.Length > 0)
            .ToList();
    }

    private static string? ReadErrorText(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object => ReadString(element, "message") ?? ReadString(element, "error"),
            _ => null,
        };
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return
            element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private string StripHtml(string value)
    {
        return parser.ParseDocument(value).Body?.TextContent.Trim() ?? value.Trim();
    }

    private static string UnexpectedResponseMessage(string body)
    {
        return $"Unexpected XenForo response to the post: {body[..Math.Min(body.Length, 200)]}";
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
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, url));
        using var response = await SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return await parser.ParseDocumentAsync(html, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        int redirectCount = 0
    )
    {
        var requestUri = request.RequestUri!;
        if (requestUri.Scheme != baseUri.Scheme || requestUri.Authority != baseUri.Authority)
        {
            throw new InvalidOperationException(
                "The forum request points to a different site. Configure the forum's final URL after redirects."
            );
        }

        request.Headers.Remove("Cookie");
        var cookieHeader = cookies.GetCookieHeader(requestUri);
        if (cookieHeader.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        var response = await http.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var cookie in setCookies)
            {
                cookies.SetCookies(requestUri, cookie);
            }
        }

        if (
            response.Headers.Location is null
            || response.StatusCode
                is not (
                    HttpStatusCode.MovedPermanently
                    or HttpStatusCode.Redirect
                    or HttpStatusCode.SeeOther
                    or HttpStatusCode.TemporaryRedirect
                    or HttpStatusCode.PermanentRedirect
                )
        )
        {
            return response;
        }

        using (response)
        {
            if (redirectCount == 10)
            {
                throw new HttpRequestException("Too many redirects from the forum.");
            }

            var redirectUri = new Uri(requestUri, response.Headers.Location);
            var useGet =
                response.StatusCode == HttpStatusCode.SeeOther
                || (
                    request.Method == HttpMethod.Post
                    && response.StatusCode
                        is HttpStatusCode.MovedPermanently
                            or HttpStatusCode.Redirect
                );

            using var redirectedRequest = new HttpRequestMessage(
                useGet ? HttpMethod.Get : request.Method,
                redirectUri
            );

            if (!useGet)
            {
                redirectedRequest.Content = request.Content;
            }

            foreach (var header in request.Headers)
            {
                redirectedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return await SendAsync(
                request: redirectedRequest,
                cancellationToken: cancellationToken,
                redirectCount: redirectCount + 1
            );
        }
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

    private static string? ExtractPostId(string postUrl)
    {
        var match = PostIdPattern().Match(postUrl);

        return match.Success ? match.Groups[1].Value : null;
    }

    private static int? ExtractThreadId(string threadUrl)
    {
        var match = ThreadIdPattern().Match(threadUrl);

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

    [GeneratedRegex(@"/threads/(?:[^/]*\.)?(\d+)(?:/|$)")]
    private static partial Regex ThreadIdPattern();

    [GeneratedRegex(@"(?:/posts/|post-)(\d+)")]
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
                Children: Children.Select(child => child.Build()).ToList(),
                StableId: ExtractNodeId(id.Value)?.ToString(CultureInfo.InvariantCulture)
            );
        }
    }
}
