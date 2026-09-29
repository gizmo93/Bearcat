using System.Net;
using Bearcat.Abstractions.Proxies;
using Shouldly;

namespace Bearcat.Abstractions.UnitTest.Proxies;

public class UnreadableProxySecretsRejectingHandlerTest
{
    private FakeProxyRoutingCache proxyRoutingCache = null!;
    private RecordingHandler innerHandler = null!;
    private HttpMessageInvoker invoker = null!;

    [SetUp]
    public void SetUp()
    {
        proxyRoutingCache = new FakeProxyRoutingCache();
        innerHandler = new RecordingHandler();
        invoker = new HttpMessageInvoker(
            new UnreadableProxySecretsRejectingHandler(
                new CategoryProxySelectingWebProxy(ProxyCategory.ImageHosters, proxyRoutingCache)
            )
            {
                InnerHandler = innerHandler,
            }
        );
    }

    [TearDown]
    public void TearDown()
    {
        invoker.Dispose();
        innerHandler.Dispose();
    }

    [Test]
    public async Task SendAsync_SelectedProxyServerHasUnreadableSecrets_ThrowsWithoutSending()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.ImageHosters,
            "http://image-proxy.test:8080",
            hasUnreadableSecrets: true
        );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            invoker.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "https://images.test/upload"),
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("Proxy for ImageHosters");
        innerHandler.SentRequestCount.ShouldBe(0);
    }

    [Test]
    public async Task SendAsync_SelectedProxyServerIsReadable_SendsRequest()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(ProxyCategory.ImageHosters, "http://image-proxy.test:8080");

        // Act
        using var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://images.test/upload"),
            CancellationToken.None
        );

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.SentRequestCount.ShouldBe(1);
    }

    [Test]
    public async Task SendAsync_ScopeSelectsCategoryWithUnreadableSecrets_ThrowsWithoutSending()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterMirrorDownloads,
            "socks5://download-proxy.test:1080",
            hasUnreadableSecrets: true
        );
        using var scope = ProxyCategoryScope.Enter(ProxyCategory.HosterMirrorDownloads);

        // Act
        var act = () =>
            invoker.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "https://images.test/upload"),
                CancellationToken.None
            );

        // Assert
        await act.ShouldThrowAsync<HttpRequestException>();
        innerHandler.SentRequestCount.ShouldBe(0);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int SentRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            SentRequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
