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

    private const string UploadFilesCapability = "upload_files";

    private const string CoverFileNameWithoutExtension = "cover";

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
            new(
                nameof(WordPressConfig.UploadCoverAsFeaturedImage),
                ConfigurationFieldType.Boolean,
                IsRequired: false,
                DefaultValue: false
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

        if (
            wordPressConfig.UploadCoverAsFeaturedImage
            && !capabilities.GetValueOrDefault(UploadFilesCapability)
        )
        {
            return new DistributionSiteLoginResult(
                Session: null,
                ErrorMessage: "The WordPress account is not allowed to upload files."
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
        IDistributionSiteConfig config,
        DistributionSession session,
        ForumTargetId target,
        string title,
        string body,
        string? coverImageUrl,
        CancellationToken cancellationToken
    )
    {
        var post = await CreatePostAsync(
            config: config,
            session: session,
            target: target,
            title: title,
            body: body,
            coverImageUrl: coverImageUrl,
            status: DraftStatus,
            cancellationToken: cancellationToken
        );

        return new PreparedDraft(
            DraftEditorUrl: $"{session.BaseUrl.TrimEnd('/')}/wp-admin/post.php?post={post.Id}&action=edit",
            RequiresSameAccountBrowserSession: true,
            PostUrl: BuildPostUrl(session, post.Id)
        );
    }

    public async Task<SubmittedPost> SubmitNewPostAsync(
        IDistributionSiteConfig config,
        DistributionSession session,
        ForumTargetId target,
        string title,
        string body,
        string? coverImageUrl,
        CancellationToken cancellationToken
    )
    {
        var post = await CreatePostAsync(
            config: config,
            session: session,
            target: target,
            title: title,
            body: body,
            coverImageUrl: coverImageUrl,
            status: PublishStatus,
            cancellationToken: cancellationToken
        );

        return new SubmittedPost(BuildPostUrl(session, post.Id));
    }

    public async Task<SubmittedPost> EditPostAsync(
        DistributionSession session,
        string postedUrl,
        string body,
        CancellationToken cancellationToken
    )
    {
        await SendRequestAsync(
            requestName: "post",
            sendRequest: () =>
                CreateApi(session)
                    .EditPostAsync(
                        id: GetPostId(postedUrl),
                        request: new EditPostRequest(Content: body, Status: null),
                        cancellationToken: cancellationToken
                    )
        );

        return new SubmittedPost(postedUrl);
    }

    public async Task<IReadOnlyList<ExistingBlogPost>> FindExistingPostsAsync(
        DistributionSession session,
        string title,
        CancellationToken cancellationToken
    )
    {
        var api = CreateApi(session);

        var currentUser = await SendRequestAsync(
            requestName: "current user request",
            sendRequest: async () =>
            {
                using var response = await api.GetCurrentUserAsync(cancellationToken);
                await response.EnsureSuccessStatusCodeAsync();
                return response.Content!;
            }
        );

        var posts = await SendRequestAsync(
            requestName: "post search",
            sendRequest: () => api.SearchPostsAsync(title, cancellationToken)
        );

        return posts
            .Where(post => string.Equals(post.Title.Raw, title, StringComparison.Ordinal))
            .Select(post => new ExistingBlogPost(
                Url: BuildPostUrl(session, post.Id),
                IsPublished: post.Status == PublishStatus,
                IsOwnedByAccount: post.Author == currentUser.Id
            ))
            .ToList();
    }

    public async Task<SubmittedPost> PublishExistingPostAsync(
        DistributionSession session,
        string postedUrl,
        string body,
        CancellationToken cancellationToken
    )
    {
        await SendRequestAsync(
            requestName: "post",
            sendRequest: () =>
                CreateApi(session)
                    .EditPostAsync(
                        id: GetPostId(postedUrl),
                        request: new EditPostRequest(Content: body, Status: PublishStatus),
                        cancellationToken: cancellationToken
                    )
        );

        return new SubmittedPost(postedUrl);
    }

    private static int GetPostId(string postedUrl)
    {
        return int.Parse(
            HttpUtility.ParseQueryString(new Uri(postedUrl).Query)["p"]!,
            CultureInfo.InvariantCulture
        );
    }

    private static string BuildPostUrl(DistributionSession session, int postId)
    {
        return $"{session.BaseUrl.TrimEnd('/')}/?p={postId}";
    }

    private async Task<PostResponse> CreatePostAsync(
        IDistributionSiteConfig config,
        DistributionSession session,
        ForumTargetId target,
        string title,
        string body,
        string? coverImageUrl,
        string status,
        CancellationToken cancellationToken
    )
    {
        int? featuredMediaId =
            config.As<WordPressConfig>().UploadCoverAsFeaturedImage && coverImageUrl is not null
                ? await UploadCoverAsync(session, coverImageUrl, cancellationToken)
                : null;

        var request = new CreatePostRequest(
            Title: title,
            Status: status,
            Categories: [int.Parse(target.Value, CultureInfo.InvariantCulture)],
            Content: body,
            FeaturedMedia: featuredMediaId
        );

        return await SendRequestAsync(
            requestName: "post",
            sendRequest: () => CreateApi(session).CreatePostAsync(request, cancellationToken)
        );
    }

    private async Task<int> UploadCoverAsync(
        DistributionSession session,
        string coverImageUrl,
        CancellationToken cancellationToken
    )
    {
        var coverUri = new Uri(coverImageUrl, UriKind.Absolute);

        if (coverUri.Scheme != Uri.UriSchemeHttp && coverUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                $"The cover URL {coverImageUrl} is not an http or https URL."
            );
        }

        using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, coverUri);
        downloadRequest.Headers.UserAgent.ParseAdd(session.UserAgent);

        using var downloadResponse = await httpClientFactory
            .CreateClient()
            .SendAsync(downloadRequest, cancellationToken);

        if (!downloadResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Unexpected response to the cover download from {coverImageUrl} (HTTP {(int)downloadResponse.StatusCode})."
            );
        }

        var contentType = downloadResponse.Content.Headers.ContentType!;
        var fileName = coverUri.Segments.Last();

        if (!Path.HasExtension(fileName))
        {
            fileName =
                $"{CoverFileNameWithoutExtension}.{contentType.MediaType!.Split('/').Last()}";
        }

        using var content = new ByteArrayContent(
            await downloadResponse.Content.ReadAsByteArrayAsync(cancellationToken)
        );

        content.Headers.ContentType = contentType;

        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = $"\"{fileName}\"",
        };

        var media = await SendRequestAsync(
            requestName: "cover upload",
            sendRequest: () => CreateApi(session).UploadMediaAsync(content, cancellationToken)
        );

        return media.Id;
    }

    private static async Task<TResponse> SendRequestAsync<TResponse>(
        string requestName,
        Func<Task<TResponse>> sendRequest
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
                $"Unexpected WordPress response to the {requestName} (HTTP {(int)exception.StatusCode}): {content[..Math.Min(content.Length, 200)]}",
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
