using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.DistributionSites.WordPress;
using Moq;
using Shouldly;

namespace Bearcat.DistributionSites.UnitTest.WordPress;

public class WordPressTest
{
    private const string BaseUrl = "https://example.org/blog";

    [Test]
    public async Task LogInAsync_SubdirectoryInstall_RequestsCurrentUserViaRestRouteWithBasicAuthorization()
    {
        // Arrange
        HttpRequestMessage? sentRequest = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(request =>
        {
            sentRequest = request;
            return CreateJsonResponse(
                HttpStatusCode.OK,
                """{"id":1,"capabilities":{"publish_posts":true}}"""
            );
        });

        var site = CreateSite(handler);

        // Act
        var result = await site.LogInAsync(CreateConfig(site), CancellationToken.None);

        // Assert
        result.ErrorMessage.ShouldBeNull();
        result.Session.ShouldNotBeNull();
        result.Session.AuthorizationHeader.ShouldBe(
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("uploader:abcd efgh ijkl"))
        );
        sentRequest.ShouldNotBeNull();
        sentRequest.RequestUri!.AbsoluteUri.ShouldBe(
            "https://example.org/blog/index.php?rest_route=/wp/v2/users/me&context=edit"
        );
        sentRequest.Headers.Authorization!.ToString().ShouldBe(result.Session.AuthorizationHeader);
    }

    [Test]
    public async Task EditPostAsync_PostedUrlWithPostId_PostsContentToRestRouteOfThatPost()
    {
        // Arrange
        HttpRequestMessage? sentRequest = null;
        string? requestBody = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(async request =>
        {
            sentRequest = request;
            requestBody = await request.Content!.ReadAsStringAsync();
            return CreateJsonResponse(HttpStatusCode.OK, """{"id":42,"status":"publish"}""");
        });

        var site = CreateSite(handler);

        // Act
        var post = await site.EditPostAsync(
            CreateSession(),
            "https://example.org/blog/?p=42",
            "<p>Updated</p>",
            CancellationToken.None
        );

        // Assert
        post.Url.ShouldBe("https://example.org/blog/?p=42");
        sentRequest.ShouldNotBeNull();
        sentRequest.Method.ShouldBe(HttpMethod.Post);
        sentRequest.RequestUri!.AbsoluteUri.ShouldBe(
            "https://example.org/blog/index.php?rest_route=/wp/v2/posts/42"
        );
        sentRequest.Headers.Authorization!.ToString().ShouldBe("Basic dXBsb2FkZXI6c2VjcmV0");
        using var json = JsonDocument.Parse(requestBody!);
        json.RootElement.GetProperty("content").GetString().ShouldBe("<p>Updated</p>");
        json.RootElement.TryGetProperty("status", out _).ShouldBeFalse();
    }

    [Test]
    public async Task FindExistingPostsAsync_FuzzySearchResult_ReturnsExactRawTitleMatchesWithOwnershipAndStatus()
    {
        // Arrange
        const string title = "Bearcat Dup & \"Probe\" \u2013 B";
        var requestedUrls = new List<string>();
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(request =>
        {
            requestedUrls.Add(request.RequestUri!.AbsoluteUri);
            return CreateJsonResponse(
                HttpStatusCode.OK,
                """{"id":3,"capabilities":{"publish_posts":true}}"""
            );
        });
        handler.Enqueue(request =>
        {
            requestedUrls.Add(request.RequestUri!.AbsoluteUri);
            return CreateJsonResponse(
                HttpStatusCode.OK,
                """
                [
                  {"id":11,"status":"publish","author":3,"title":{"raw":"Bearcat Dup & \"Probe\" \u2013 B","rendered":"Bearcat Dup &#038; &#171;Probe&#187; &#8211; B"}},
                  {"id":12,"status":"draft","author":9,"title":{"raw":"Bearcat Dup & \"Probe\" \u2013 B","rendered":"Bearcat Dup &#038; &#171;Probe&#187; &#8211; B"}},
                  {"id":13,"status":"publish","author":3,"title":{"raw":"Bearcat Dup & \"Probe\" \u2013 B.Extended","rendered":"Bearcat Dup &#038; &#171;Probe&#187; &#8211; B.Extended"}},
                  {"id":14,"status":"publish","author":3,"title":{"raw":"bearcat dup & \"probe\" \u2013 b","rendered":"bearcat dup &#038; &#171;probe&#187; &#8211; b"}}
                ]
                """
            );
        });

        var site = CreateSite(handler);

        // Act
        var posts = await site.FindExistingPostsAsync(
            CreateSession(),
            title,
            CancellationToken.None
        );

        // Assert
        requestedUrls.ShouldBe([
            "https://example.org/blog/index.php?rest_route=/wp/v2/users/me&context=edit",
            "https://example.org/blog/index.php?rest_route=/wp/v2/posts&search=Bearcat%20Dup%20%26%20%22Probe%22%20%E2%80%93%20B&status=any&context=edit&per_page=100&_fields=id,status,author,title",
        ]);
        posts.ShouldBe([
            new ExistingBlogPost(
                Url: "https://example.org/blog/?p=11",
                IsPublished: true,
                IsOwnedByAccount: true
            ),
            new ExistingBlogPost(
                Url: "https://example.org/blog/?p=12",
                IsPublished: false,
                IsOwnedByAccount: false
            ),
        ]);
    }

    [Test]
    public async Task PublishExistingPostAsync_PostedUrlWithPostId_PostsContentAndPublishStatusToThatPost()
    {
        // Arrange
        HttpRequestMessage? sentRequest = null;
        string? requestBody = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(async request =>
        {
            sentRequest = request;
            requestBody = await request.Content!.ReadAsStringAsync();
            return CreateJsonResponse(HttpStatusCode.OK, """{"id":42,"status":"publish"}""");
        });

        var site = CreateSite(handler);

        // Act
        var post = await site.PublishExistingPostAsync(
            CreateSession(),
            "https://example.org/blog/?p=42",
            "<p>Adopted</p>",
            CancellationToken.None
        );

        // Assert
        post.Url.ShouldBe("https://example.org/blog/?p=42");
        sentRequest.ShouldNotBeNull();
        sentRequest.Method.ShouldBe(HttpMethod.Post);
        sentRequest.RequestUri!.AbsoluteUri.ShouldBe(
            "https://example.org/blog/index.php?rest_route=/wp/v2/posts/42"
        );
        using var json = JsonDocument.Parse(requestBody!);
        json.RootElement.GetProperty("content").GetString().ShouldBe("<p>Adopted</p>");
        json.RootElement.GetProperty("status").GetString().ShouldBe("publish");
    }

    [Test]
    public async Task GetTargetHierarchyAsync_CategoriesOnTwoPages_ReturnsTreeWithDecodedNames()
    {
        // Arrange
        var requestedUrls = new List<string>();
        var firstPage = Enumerable
            .Range(1, 100)
            .Select(id => (Id: id, Name: id == 1 ? "A &amp; B" : $"Category {id}"))
            .Select(category =>
                $$"""{"id":{{category.Id}},"name":"{{category.Name}}","parent":0}"""
            )
            .ToList();

        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(request =>
        {
            requestedUrls.Add(request.RequestUri!.AbsoluteUri);
            return CreateJsonResponse(HttpStatusCode.OK, $"[{string.Join(",", firstPage)}]");
        });
        handler.Enqueue(request =>
        {
            requestedUrls.Add(request.RequestUri!.AbsoluteUri);
            return CreateJsonResponse(
                HttpStatusCode.OK,
                """[{"id":101,"name":"Child","parent":1},{"id":102,"name":"Grandchild","parent":101}]"""
            );
        });

        var site = CreateSite(handler);

        // Act
        var nodes = await site.GetTargetHierarchyAsync(CreateSession(), CancellationToken.None);

        // Assert
        requestedUrls.ShouldBe([
            "https://example.org/blog/index.php?rest_route=/wp/v2/categories&per_page=100&_fields=id,name,parent&page=1",
            "https://example.org/blog/index.php?rest_route=/wp/v2/categories&per_page=100&_fields=id,name,parent&page=2",
        ]);
        nodes.Count.ShouldBe(100);

        var firstNode = nodes.First();
        firstNode.Id.ShouldBe(new ForumTargetId("1"));
        firstNode.Title.ShouldBe("A & B");
        firstNode.CanReceivePosts.ShouldBeTrue();
        firstNode.NumericNodeId.ShouldBe("1");

        var child = firstNode.Children.ShouldHaveSingleItem();
        child.Id.ShouldBe(new ForumTargetId("101"));
        child.Title.ShouldBe("Child");
        child.Children.ShouldHaveSingleItem().Id.ShouldBe(new ForumTargetId("102"));
        nodes.Last().Children.ShouldBeEmpty();
    }

    [Test]
    public async Task LogInAsync_Unauthorized_ReturnsErrorWithoutSession()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ =>
            CreateJsonResponse(
                HttpStatusCode.Unauthorized,
                """{"code":"rest_not_logged_in","message":"Du bist derzeit nicht angemeldet.","data":{"status":401}}"""
            )
        );

        var site = CreateSite(handler);

        // Act
        var result = await site.LogInAsync(CreateConfig(site), CancellationToken.None);

        // Assert
        result.Session.ShouldBeNull();
        result.ErrorMessage.ShouldBe("WordPress rejected the username or application password.");
    }

    [Test]
    public async Task LogInAsync_WithoutPublishPostsCapability_ReturnsErrorWithoutSession()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ =>
            CreateJsonResponse(
                HttpStatusCode.OK,
                """{"id":2,"capabilities":{"read":true,"edit_posts":true}}"""
            )
        );

        var site = CreateSite(handler);

        // Act
        var result = await site.LogInAsync(CreateConfig(site), CancellationToken.None);

        // Assert
        result.Session.ShouldBeNull();
        result.ErrorMessage.ShouldBe("The WordPress account is not allowed to publish posts.");
    }

    [Test]
    public async Task LogInAsync_UploadCoverOnWithoutUploadFilesCapability_ReturnsErrorWithoutSession()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ =>
            CreateJsonResponse(
                HttpStatusCode.OK,
                """{"id":2,"capabilities":{"publish_posts":true}}"""
            )
        );

        var site = CreateSite(handler);

        // Act
        var result = await site.LogInAsync(
            CreateConfig(site) with
            {
                UploadCoverAsFeaturedImage = true,
            },
            CancellationToken.None
        );

        // Assert
        result.Session.ShouldBeNull();
        result.ErrorMessage.ShouldBe("The WordPress account is not allowed to upload files.");
    }

    [Test]
    public async Task SubmitNewPostAsync_UploadCoverOnWithCoverUrl_UploadsCoverAndSetsFeaturedMedia()
    {
        // Arrange
        var coverBytes = new byte[] { 137, 80, 78, 71 };
        HttpRequestMessage? coverRequest = null;
        HttpRequestMessage? mediaRequest = null;
        byte[]? mediaBody = null;
        string? postBody = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(request =>
        {
            coverRequest = request;
            var content = new ByteArrayContent(coverBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        handler.Enqueue(async request =>
        {
            mediaRequest = request;
            mediaBody = await request.Content!.ReadAsByteArrayAsync();
            return CreateJsonResponse(HttpStatusCode.Created, """{"id":77,"source_url":"x"}""");
        });
        handler.Enqueue(async request =>
        {
            postBody = await request.Content!.ReadAsStringAsync();
            return CreateJsonResponse(HttpStatusCode.Created, """{"id":42}""");
        });

        var site = CreateSite(handler);

        // Act
        var post = await site.SubmitNewPostAsync(
            config: CreateConfig(site) with
            {
                UploadCoverAsFeaturedImage = true,
            },
            session: CreateSession(),
            target: new ForumTargetId("3"),
            title: "Release.Name",
            body: "<p>Body</p>",
            coverImageUrl: "https://images.example.net/t/p/original/poster.png",
            cancellationToken: CancellationToken.None
        );

        // Assert
        post.Url.ShouldBe("https://example.org/blog/?p=42");
        coverRequest.ShouldNotBeNull();
        coverRequest.RequestUri!.AbsoluteUri.ShouldBe(
            "https://images.example.net/t/p/original/poster.png"
        );
        coverRequest.Headers.Authorization.ShouldBeNull();
        mediaRequest.ShouldNotBeNull();
        mediaRequest.Method.ShouldBe(HttpMethod.Post);
        mediaRequest.RequestUri!.AbsoluteUri.ShouldBe(
            "https://example.org/blog/index.php?rest_route=/wp/v2/media"
        );
        mediaRequest.Headers.Authorization!.ToString().ShouldBe("Basic dXBsb2FkZXI6c2VjcmV0");
        mediaRequest.Content!.Headers.ContentType!.MediaType.ShouldBe("image/png");
        mediaRequest
            .Content.Headers.ContentDisposition!.ToString()
            .ShouldBe("attachment; filename=\"poster.png\"");
        mediaBody.ShouldBe(coverBytes);
        using var json = JsonDocument.Parse(postBody!);
        json.RootElement.GetProperty("featured_media").GetInt32().ShouldBe(77);
    }

    [TestCase(false, "https://images.example.net/poster.png")]
    [TestCase(true, null)]
    public async Task SubmitNewPostAsync_UploadCoverOffOrNoCoverUrl_PostsWithoutFeaturedMedia(
        bool uploadCoverAsFeaturedImage,
        string? coverImageUrl
    )
    {
        // Arrange
        var requestedUrls = new List<string>();
        string? postBody = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(async request =>
        {
            requestedUrls.Add(request.RequestUri!.AbsoluteUri);
            postBody = await request.Content!.ReadAsStringAsync();
            return CreateJsonResponse(HttpStatusCode.Created, """{"id":42}""");
        });

        var site = CreateSite(handler);

        // Act
        await site.SubmitNewPostAsync(
            config: CreateConfig(site) with
            {
                UploadCoverAsFeaturedImage = uploadCoverAsFeaturedImage,
            },
            session: CreateSession(),
            target: new ForumTargetId("3"),
            title: "Release.Name",
            body: "<p>Body</p>",
            coverImageUrl: coverImageUrl,
            cancellationToken: CancellationToken.None
        );

        // Assert
        requestedUrls.ShouldBe(["https://example.org/blog/index.php?rest_route=/wp/v2/posts"]);
        using var json = JsonDocument.Parse(postBody!);
        json.RootElement.TryGetProperty("featured_media", out _).ShouldBeFalse();
    }

    private static DistributionSites.WordPress.WordPress CreateSite(HttpMessageHandler handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactory
            .Setup(factory =>
                factory.CreateClient(DistributionSites.WordPress.WordPress.HttpClientName)
            )
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        httpClientFactory
            .Setup(factory => factory.CreateClient(string.Empty))
            .Returns(() => new HttpClient(handler, disposeHandler: false));

        return new DistributionSites.WordPress.WordPress(httpClientFactory.Object);
    }

    private static WordPressConfig CreateConfig(DistributionSites.WordPress.WordPress site)
    {
        return (WordPressConfig)
            site.DeserializeConfig(
                $$"""{"baseUrl":"{{BaseUrl}}","username":"uploader","applicationPassword":"abcd efgh ijkl"}"""
            );
    }

    private static DistributionSession CreateSession()
    {
        return new DistributionSession(
            BaseUrl: BaseUrl,
            UserAgent: "Bearcat",
            Cookies: [],
            AuthorizationHeader: "Basic dXBsb2FkZXI6c2VjcmV0"
        );
    }

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> responses =
            new();

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            responses.Enqueue(request => Task.FromResult(responseFactory(request)));
        }

        public void Enqueue(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
        {
            responses.Enqueue(responseFactory);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            responses.Count.ShouldBeGreaterThan(0);

            var response = await responses.Dequeue()(request);
            response.RequestMessage ??= request;
            return response;
        }
    }
}
