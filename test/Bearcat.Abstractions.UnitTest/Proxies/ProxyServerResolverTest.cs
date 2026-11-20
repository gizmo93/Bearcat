using Bearcat.Abstractions.Proxies;
using Shouldly;

namespace Bearcat.Abstractions.UnitTest.Proxies;

public class ProxyServerResolverTest
{
    private FakeProxyRoutingCache proxyRoutingCache = null!;

    [SetUp]
    public void SetUp()
    {
        proxyRoutingCache = new FakeProxyRoutingCache();
    }

    [Test]
    public void ResolveForClientCategory_NoScope_ReturnsProxyServerOfClientCategory()
    {
        // Arrange
        var uploadProxyServer = proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );

        // Act
        var proxyServer = ProxyServerResolver.ResolveForClientCategory(
            ProxyCategory.HosterUploads,
            proxyRoutingCache
        );

        // Assert
        proxyServer.ShouldBe(uploadProxyServer);
    }

    [Test]
    public void ResolveForClientCategory_ScopeOfUnrelatedCategory_ReturnsProxyServerOfClientCategory()
    {
        // Arrange
        var uploadProxyServer = proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );
        proxyRoutingCache.AddProxyServer(7, "http://specific-proxy.test:3128");

        using var scope = ProxyCategoryScope.Enter(
            ProxyCategory.ImageHosters,
            ProxySelection.SpecificProxyServer,
            proxyServerId: 7
        );

        // Act
        var proxyServer = ProxyServerResolver.ResolveForClientCategory(
            ProxyCategory.HosterUploads,
            proxyRoutingCache
        );

        // Assert
        proxyServer.ShouldBe(uploadProxyServer);
    }

    [Test]
    public void ResolveForClientCategory_MirrorDownloadScopeAndUploadClient_ReturnsProxyServerOfScopeCategory()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );
        var downloadProxyServer = proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterMirrorDownloads,
            "socks5://download-proxy.test:1080"
        );

        using var scope = ProxyCategoryScope.Enter(
            ProxyCategory.HosterMirrorDownloads,
            ProxySelection.UseCategoryDefault,
            proxyServerId: null
        );

        // Act
        var proxyServer = ProxyServerResolver.ResolveForClientCategory(
            ProxyCategory.HosterUploads,
            proxyRoutingCache
        );

        // Assert
        proxyServer.ShouldBe(downloadProxyServer);
    }

    [Test]
    public void ResolveForClientCategory_ScopeOfSameCategoryWithNoProxy_ReturnsNull()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );

        using var scope = ProxyCategoryScope.Enter(
            ProxyCategory.HosterUploads,
            ProxySelection.NoProxy,
            proxyServerId: null
        );

        // Act
        var proxyServer = ProxyServerResolver.ResolveForClientCategory(
            ProxyCategory.HosterUploads,
            proxyRoutingCache
        );

        // Assert
        proxyServer.ShouldBeNull();
    }

    [Test]
    public void ResolveForClientCategory_ScopeOfSameCategoryWithSpecificProxyServer_ReturnsThatProxyServer()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );
        var specificProxyServer = proxyRoutingCache.AddProxyServer(
            7,
            "http://specific-proxy.test:3128"
        );

        using var scope = ProxyCategoryScope.Enter(
            ProxyCategory.HosterUploads,
            ProxySelection.SpecificProxyServer,
            proxyServerId: 7
        );

        // Act
        var proxyServer = ProxyServerResolver.ResolveForClientCategory(
            ProxyCategory.HosterUploads,
            proxyRoutingCache
        );

        // Assert
        proxyServer.ShouldBe(specificProxyServer);
    }

    [Test]
    public void ResolveForCurrentScope_NoScope_ReturnsNull()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );

        // Act
        var proxyServer = ProxyServerResolver.ResolveForCurrentScope(proxyRoutingCache);

        // Assert
        proxyServer.ShouldBeNull();
    }
}
