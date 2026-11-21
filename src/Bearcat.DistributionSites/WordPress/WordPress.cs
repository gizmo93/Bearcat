using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.DistributionSite;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Abstractions.DistributionSite.Results;
using Bearcat.DistributionSites.Extensions;
using Bearcat.DistributionSites.WordPress.Api;
using Refit;

namespace Bearcat.DistributionSites.WordPress;

public sealed class WordPress(IHttpClientFactory httpClientFactory) : IBlogDistributionSite
{
    public const string HttpClientName = "WordPress";

    private const string UserAgent = "Bearcat";

    private const int CategoriesPerPage = 100;

    private const string DraftStatus = "draft";

    private const string PublishStatus = "publish";

    private const string PublishPostsCapability = "publish_posts";

    private static readonly JsonSerializerOptions ConfigSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Name => "WordPress";

    public PostContentFormat ContentFormat => PostContentFormat.Html;

    public IReadOnlyList<ConfigurationField> ConfigurationFields =>
        [
            new(nameof(WordPressConfig.BaseUrl), ConfigurationFieldType.Url, IsRequired: true),
            new(nameof(WordPressConfig.Username), ConfigurationFieldType.Text, IsRequired: true),
            new(
                nameof(WordPressConfig.ApplicationPassword),
                ConfigurationFieldType.Password,
                IsRequired: true
            ),
        ];

    public IDistributionSiteConfig DeserializeConfig(string serializedConfig)
    {
        return JsonSerializer.Deserialize<WordPressConfig>(
                serializedConfig,
                ConfigSerializerOptions
            )
            ?? throw new InvalidOperationException(
                $"Could not deserialize the {nameof(WordPressConfig)} configuration."
            );
    }

    public string GetBaseUrl(IDistributionSiteConfig config) =>
        config.As<WordPressConfig>().BaseUrl;

    public async Task<DistributionSiteLoginResult> LogInAsync(
        IDistributionSiteConfig config,
        CancellationToken cancellationToken
    )
    {
        var wordPressConfig = config.As<WordPressConfig>();

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                $"{wordPressConfig.Username}:{wordPressConfig.ApplicationPassword}"
            )
        );

        var session = new DistributionSession(
            BaseUrl: wordPressConfig.BaseUrl,
            UserAgent: UserAgent,
            Cookies: [],
            AuthorizationHeader: $"Basic {credentials}"
        );

        using var response = await CreateApi(session).GetCurrentUserAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new DistributionSiteLoginResult(
                Session: null,
                ErrorMessage: "WordPress rejected the username or application password."
            );
        }

        if (!response.IsSuccessStatusCode || response.Content?.Capabilities is not { } capabilities)
        {
            return new DistributionSiteLoginResult(
                Session: null,
                ErrorMessage: $"The WordPress REST API at {session.BaseUrl} did not answer as expected (HTTP {(int?)response.StatusCode})."
            );
        }

        if (!capabilities.GetValueOrDefault(PublishPostsCapability))
        {
            return new DistributionSiteLoginResult(
                Session: null,
                ErrorMessage: "The WordPress account is not allowed to publish posts."
            );
        }

        return new DistributionSiteLoginResult(Session: session, ErrorMessage: null);
    }

    public async Task<bool> IsSessionValidAsync(
        DistributionSession session,
        CancellationToken cancellationToken
    )
    {
        using var response = await CreateApi(session).GetCurrentUserAsync(cancellationToken);
        return response.StatusCode == HttpStatusCode.OK;
    }

    public async Task<IReadOnlyList<ForumTargetNode>> GetTargetHierarchyAsync(
        DistributionSession session,
        CancellationToken cancellationToken
    )
    {
        var api = CreateApi(session);
        var categories = new List<CategoryResponse>();
        var page = 1;
        IReadOnlyList<CategoryResponse> categoriesOfPage;

        do
        {
            categoriesOfPage = await api.GetCategoriesAsync(
                perPage: CategoriesPerPage,
                page: page,
                cancellationToken: cancellationToken
            );

            categories.AddRange(categoriesOfPage);
            page++;
        } while (categoriesOfPage.Count == CategoriesPerPage);

        return BuildCategoryNodes(
            categoriesByParentId: categories.ToLookup(category => category.Parent),
            parentId: 0
        );
    }

    public ForumTargetId CreateForumTargetIdFromStoredValue(
        DistributionSession session,
        string storedTargetId
    )
    {
        return new ForumTargetId(storedTargetId.Trim());
    }

    public async Task<PreparedDraft> PrepareNewPostDraftAsync(
        DistributionSession session,
        ForumTargetId target,
        string title,
        string body,
        CancellationToken cancellationToken
    )
    {
        var post = await CreatePostAsync(
            session: session,
            target: target,
            title: title,
            body: body,
            status: DraftStatus,
            cancellationToken: cancellationToken
        );

        return new PreparedDraft(
            DraftEditorUrl: $"{session.BaseUrl.TrimEnd('/')}/wp-admin/post.php?post={post.Id}&action=edit",
            RequiresSameAccountBrowserSession: true
        );
    }

    public async Task<SubmittedPost> SubmitNewPostAsync(
        DistributionSession session,
        ForumTargetId target,
        string title,
        string body,
        CancellationToken cancellationToken
    )
    {
        var post = await CreatePostAsync(
            session: session,
            target: target,
            title: title,
            body: body,
            status: PublishStatus,
            cancellationToken: cancellationToken
        );

        return new SubmittedPost($"{session.BaseUrl.TrimEnd('/')}/?p={post.Id}");
    }

    public async Task<SubmittedPost> EditPostAsync(
        DistributionSession session,
        string postedUrl,
        string body,
        CancellationToken cancellationToken
    )
    {
        var postId = int.Parse(
            HttpUtility.ParseQueryString(new Uri(postedUrl).Query)["p"]!,
            CultureInfo.InvariantCulture
        );

        await SendPostRequestAsync(() =>
            CreateApi(session)
                .EditPostAsync(
                    id: postId,
                    request: new EditPostRequest(body),
                    cancellationToken: cancellationToken
                )
        );

        return new SubmittedPost(postedUrl);
    }

    private async Task<PostResponse> CreatePostAsync(
        DistributionSession session,
        ForumTargetId target,
        string title,
        string body,
        string status,
        CancellationToken cancellationToken
    )
    {
        var request = new CreatePostRequest(
            Title: title,
            Status: status,
            Categories: [int.Parse(target.Value, CultureInfo.InvariantCulture)],
            Content: body
        );

        return await SendPostRequestAsync(() =>
            CreateApi(session).CreatePostAsync(request, cancellationToken)
        );
    }

    private static async Task<PostResponse> SendPostRequestAsync(
        Func<Task<PostResponse>> sendRequest
    )
    {
        try
        {
            return await sendRequest();
        }
        catch (ApiException exception)
        {
            var content = exception.Content ?? string.Empty;

            throw new InvalidOperationException(
                $"Unexpected WordPress response to the post (HTTP {(int)exception.StatusCode}): {content[..Math.Min(content.Length, 200)]}",
                exception
            );
        }
    }

    private static List<ForumTargetNode> BuildCategoryNodes(
        ILookup<int, CategoryResponse> categoriesByParentId,
        int parentId
    )
    {
        return categoriesByParentId[parentId]
            .Select(category => new ForumTargetNode(
                Id: new ForumTargetId(category.Id.ToString(CultureInfo.InvariantCulture)),
                Title: WebUtility.HtmlDecode(category.Name),
                CanReceivePosts: true,
                Children: BuildCategoryNodes(categoriesByParentId, category.Id),
                NumericNodeId: category.Id.ToString(CultureInfo.InvariantCulture)
            ))
            .ToList();
    }

    private IWordPressApi CreateApi(DistributionSession session)
    {
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        httpClient.BaseAddress = new Uri(session.BaseUrl);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(session.UserAgent);

        httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(
            session.AuthorizationHeader!
        );

        return RestService.For<IWordPressApi>(httpClient);
    }
}
