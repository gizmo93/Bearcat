using System.Net;
using System.Text;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.DistributionSites.Shared.XenForo.Api;
using Moq;
using Shouldly;

namespace Bearcat.DistributionSites.UnitTest.Shared.XenForo.Api;

public class XenForoForumClientTest
{
    [Test]
    public async Task SearchThreadsAsync_WithForumUrl_SearchesPostsInSelectedNode()
    {
        // Arrange
        string? requestBody = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse("<html data-csrf=\"token\"></html>"));
        handler.Enqueue(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            return HtmlResponse("<html></html>");
        });

        using var client = CreateClient(handler);

        // Act
        await client.SearchThreadsAsync(
            "Ich Weiss Was Du Letzten Sommer Getan Hast",
            "https://www.data-load.me/forums/uhd-4k.9/",
            CancellationToken.None
        );

        // Assert
        requestBody.ShouldNotBeNull();
        requestBody.ShouldContain("search_type=post");
        requestBody.ShouldContain("c%5Bnodes%5D%5B0%5D=9");
    }

    [Test]
    public async Task GetForumTreeAsync_ForumWithNodeIdInUrl_ExposesStableId()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ =>
            HtmlResponse(
                """
                <html>
                    <div class="node node--category">
                        <div class="node-title"><a href="/forums/movies.4/">Movies</a></div>
                    </div>
                    <div class="node node--forum">
                        <div class="node-title"><a href="/forums/uhd-4k.9/">UHD 4K</a></div>
                    </div>
                    <div class="node node--forum">
                        <div class="node-title"><a href="/pages/rules/">Rules</a></div>
                    </div>
                </html>
                """
            )
        );

        using var client = CreateClient(handler);

        // Act
        var tree = await client.GetForumTreeAsync(CancellationToken.None);

        // Assert
        var category = tree.ShouldHaveSingleItem();
        category.StableId.ShouldBe("4");
        category.Children.Count.ShouldBe(2);
        category.Children[0].StableId.ShouldBe("9");
        category.Children[1].StableId.ShouldBeNull();
    }

    [Test]
    public async Task SubmitNewThreadAsync_FormAccepted_PostsAllFieldsAndReturnsRedirect()
    {
        // Arrange
        string? requestBody = null;
        string? requestUri = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(NewThreadPage));
        handler.Enqueue(async request =>
        {
            requestUri = request.RequestUri?.ToString();
            requestBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(
                """
                {"status":"ok","redirect":"/threads/my-release.42/"}
                """
            );
        });

        using var client = CreateClient(handler);

        // Act
        var submitted = await client.SubmitNewThreadAsync(
            forumUrl: "https://www.data-load.me/forums/uhd-4k.9",
            title: "My Release",
            prefixIds: ["3", "7"],
            message: "Body text",
            cancellationToken: CancellationToken.None
        );

        // Assert
        submitted.Url.ShouldBe("https://www.data-load.me/threads/my-release.42/");
        requestUri.ShouldBe("https://www.data-load.me/forums/uhd-4k.9/post-thread");
        requestBody.ShouldNotBeNull();
        requestBody.ShouldContain("_xfToken=form-token");
        requestBody.ShouldContain("attachment_hash=hash-123");
        requestBody.ShouldContain("last_date=1700000000");
        requestBody.ShouldContain("kzOb4=");
        requestBody.ShouldContain("title=My+Release");
        requestBody.ShouldContain("message=Body+text");
        requestBody.ShouldContain("prefix_id%5B%5D=3");
        requestBody.ShouldContain("prefix_id%5B%5D=7");
        requestBody.ShouldContain("_xfResponseType=json");
        requestBody.ShouldNotContain("_xfResponseType=html");
        requestBody.ShouldNotContain("title=Stale+Draft");
    }

    [Test]
    public async Task SubmitNewThreadAsync_ResponseWithErrors_ThrowsWithPlainTextErrors()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(NewThreadPage));
        handler.Enqueue(_ =>
            JsonResponse(
                """
                {"status":"error","errors":["Please enter a <b>title</b>.","Flood control is active."]}
                """
            )
        );

        using var client = CreateClient(handler);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            client.SubmitNewThreadAsync(
                forumUrl: "https://www.data-load.me/forums/uhd-4k.9/",
                title: string.Empty,
                prefixIds: [],
                message: "Body text",
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("Please enter a title.");
        exception.Message.ShouldContain("Flood control is active.");
        exception.Message.ShouldNotContain("<b>");
    }

    [Test]
    public async Task SubmitNewThreadAsync_PageWithoutForm_ThrowsDescriptiveException()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ =>
            HtmlResponse("<html data-csrf=\"token\"><div>You may not post here.</div></html>")
        );

        using var client = CreateClient(handler);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            client.SubmitNewThreadAsync(
                forumUrl: "https://www.data-load.me/forums/uhd-4k.9/",
                title: "My Release",
                prefixIds: [],
                message: "Body text",
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("post-thread");
        exception.Message.ShouldContain("https://www.data-load.me/forums/uhd-4k.9/post-thread");
    }

    [Test]
    public async Task SubmitReplyAsync_FormAccepted_PostsHiddenFieldsAndReturnsRedirect()
    {
        // Arrange
        string? requestBody = null;
        string? requestUri = null;
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(ThreadPage));
        handler.Enqueue(async request =>
        {
            requestUri = request.RequestUri?.ToString();
            requestBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(
                """
                {"status":"ok","redirect":"https://www.data-load.me/threads/my-release.42/post-99"}
                """
            );
        });

        using var client = CreateClient(handler);

        // Act
        var submitted = await client.SubmitReplyAsync(
            threadUrl: "https://www.data-load.me/threads/my-release.42/",
            message: "Reupped",
            cancellationToken: CancellationToken.None
        );

        // Assert
        submitted.Url.ShouldBe("https://www.data-load.me/threads/my-release.42/post-99");
        requestUri.ShouldBe("https://www.data-load.me/threads/my-release.42/add-reply");
        requestBody.ShouldNotBeNull();
        requestBody.ShouldContain("_xfToken=form-token");
        requestBody.ShouldContain("attachment_hash=hash-456");
        requestBody.ShouldContain("message=Reupped");
        requestBody.ShouldContain("_xfResponseType=json");
        requestBody.ShouldNotContain("message=Stale+Draft");
    }

    [Test]
    public async Task SubmitReplyAsync_ResponseWithErrors_ThrowsWithPlainTextErrors()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(_ => HtmlResponse(ThreadPage));
        handler.Enqueue(_ =>
            JsonResponse(
                """
                {"status":"error","errors":{"message":"This thread is closed."}}
                """
            )
        );

        using var client = CreateClient(handler);

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            client.SubmitReplyAsync(
                threadUrl: "https://www.data-load.me/threads/my-release.42/",
                message: "Reupped",
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("This thread is closed.");
    }

    private const string NewThreadPage = """
        <html data-csrf="page-token">
            <form action="/forums/uhd-4k.9/post-thread" method="post">
                <input type="hidden" name="_xfToken" value="form-token">
                <input type="hidden" name="attachment_hash" value="hash-123">
                <input type="hidden" name="last_date" value="1700000000">
                <input type="hidden" name="kzOb4" value="">
                <input type="hidden" name="_xfResponseType" value="html">
                <input type="hidden" name="title" value="Stale Draft">
                <select name="prefix_id[]"><option value="3">Movie</option></select>
            </form>
        </html>
        """;

    private const string ThreadPage = """
        <html data-csrf="page-token">
            <form action="/threads/my-release.42/add-reply" method="post">
                <input type="hidden" name="_xfToken" value="form-token">
                <input type="hidden" name="attachment_hash" value="hash-456">
                <input type="hidden" name="message" value="Stale Draft">
            </form>
        </html>
        """;

    private static XenForoForumClient CreateClient(HttpMessageHandler handler)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactory
            .Setup(factory => factory.CreateClient(XenForoForumClient.HttpClientName))
            .Returns(() => new HttpClient(handler, disposeHandler: false));

        return new XenForoForumClient(
            httpClientFactory.Object,
            new Uri("https://www.data-load.me/"),
            new DistributionSession("Bearcat", [])
        );
    }

    private static HttpResponseMessage HtmlResponse(string html)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html"),
        };
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            responses.Count.ShouldBeGreaterThan(0);
            return responses.Dequeue()(request);
        }
    }
}
