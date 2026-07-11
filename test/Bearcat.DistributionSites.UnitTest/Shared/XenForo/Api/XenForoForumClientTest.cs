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

        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactory
            .Setup(factory => factory.CreateClient(XenForoForumClient.HttpClientName))
            .Returns(() => new HttpClient(handler, disposeHandler: false));

        using var client = new XenForoForumClient(
            httpClientFactory.Object,
            new Uri("https://www.data-load.me/"),
            new DistributionSession("Bearcat", [])
        );

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

    private static HttpResponseMessage HtmlResponse(string html)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html"),
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
