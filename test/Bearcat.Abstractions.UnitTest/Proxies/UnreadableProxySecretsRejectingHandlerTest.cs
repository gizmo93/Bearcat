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
    public async Task SendAsync_ScopeSelectsCategoryOfSameGroupWithUnreadableSecrets_ThrowsWithoutSending()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterMirrorDownloads,
            "socks5://download-proxy.test:1080",
            hasUnreadableSecrets: true
        );
        using var hosterUploadInnerHandler = new RecordingHandler();
        using var hosterUploadInvoker = new HttpMessageInvoker(
            new UnreadableProxySecretsRejectingHandler(
                new CategoryProxySelectingWebProxy(ProxyCategory.HosterUploads, proxyRoutingCache)
            )
            {
                InnerHandler = hosterUploadInnerHandler,
            }
        );
        using var scope = ProxyCategoryScope.Enter(ProxyCategory.HosterMirrorDownloads);

        // Act
        var act = () =>
            hosterUploadInvoker.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "https://mirror.test/file"),
                CancellationToken.None
            );

        // Assert
        await act.ShouldThrowAsync<HttpRequestException>();
        hosterUploadInnerHandler.SentRequestCount.ShouldBe(0);
    }

    [Test]
    public async Task SendAsync_ScopeSelectsSpecificProxyServerWithUnreadableSecrets_ThrowsWithoutSending()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(ProxyCategory.ImageHosters, "http://image-proxy.test:8080");
        proxyRoutingCache.AddProxyServer(
            7,
            "http://specific-proxy.test:3128",
            hasUnreadableSecrets: true
        );
        using var scope = ProxyCategoryScope.Enter(
            ProxyCategory.ImageHosters,
            ProxySelection.SpecificProxyServer,
            proxyServerId: 7
        );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            invoker.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "https://images.test/upload"),
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("Proxy server 7");
        innerHandler.SentRequestCount.ShouldBe(0);
    }

    [Test]
    public async Task SendAsync_ScopeOfOtherGroupSelectsUnreadableProxyServer_SendsRequest()
    {
        // Arrange
        proxyRoutingCache.AddProxyServer(
            7,
            "http://specific-proxy.test:3128",
            hasUnreadableSecrets: true
        );
        using var scope = ProxyCategoryScope.Enter(
            ProxyCategory.HosterUploads,
            ProxySelection.SpecificProxyServer,
            proxyServerId: 7
        );

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
    public async Task SendAsync_ScopeSelectsNoProxyWhileCategoryDefaultIsUnreadable_SendsRequest()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.ImageHosters,
            "http://image-proxy.test:8080",
            hasUnreadableSecrets: true
        );
        using var scope = ProxyCategoryScope.Enter(
            ProxyCategory.ImageHosters,
            ProxySelection.NoProxy,
            proxyServerId: null
        );

        // Act
        using var response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://images.test/upload"),
            CancellationToken.None
        );

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.SentRequestCount.ShouldBe(1);
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
