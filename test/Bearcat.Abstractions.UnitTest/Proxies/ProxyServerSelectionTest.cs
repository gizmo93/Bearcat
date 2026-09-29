using Bearcat.Abstractions.Proxies;
using Shouldly;

namespace Bearcat.Abstractions.UnitTest.Proxies;

public class ProxyServerSelectionTest
{
    private FakeProxyRoutingCache proxyRoutingCache = null!;

    [SetUp]
    public void SetUp()
    {
        proxyRoutingCache = new FakeProxyRoutingCache();
    }

    [Test]
    public void Select_NoScope_ReturnsProxyServerOfClientCategory()
    {
        // Arrange
        var uploadProxyServer = proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );

        // Act
        var proxyServer = ProxyServerSelection.Select(
            ProxyCategory.HosterUploads,
            scopeState: null,
            proxyRoutingCache
        );

        // Assert
        proxyServer.ShouldBe(uploadProxyServer);
    }

    [Test]
    public void Select_ScopeOfOtherGroup_ReturnsProxyServerOfClientCategory()
    {
        // Arrange
        var uploadProxyServer = proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );
        proxyRoutingCache.AddProxyServer(7, "http://specific-proxy.test:3128");
        var scopeState = new ProxyCategoryScopeState(
            ProxyCategory.ImageHosters,
            ProxySelection.SpecificProxyServer,
            ProxyServerId: 7
        );

        // Act
        var proxyServer = ProxyServerSelection.Select(
            ProxyCategory.HosterUploads,
            scopeState,
            proxyRoutingCache
        );

        // Assert
        proxyServer.ShouldBe(uploadProxyServer);
    }

    [Test]
    public void Select_ScopeOfSameGroupWithCategoryDefault_ReturnsProxyServerOfScopeCategory()
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
        var scopeState = new ProxyCategoryScopeState(
            ProxyCategory.HosterMirrorDownloads,
            ProxySelection.UseCategoryDefault,
            ProxyServerId: null
        );

        // Act
        var proxyServer = ProxyServerSelection.Select(
            ProxyCategory.HosterUploads,
            scopeState,
            proxyRoutingCache
        );

        // Assert
        proxyServer.ShouldBe(downloadProxyServer);
    }

    [Test]
    public void Select_ScopeOfSameGroupWithNoProxy_ReturnsNull()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );
        var scopeState = new ProxyCategoryScopeState(
            ProxyCategory.HosterUploads,
            ProxySelection.NoProxy,
            ProxyServerId: null
        );

        // Act
        var proxyServer = ProxyServerSelection.Select(
            ProxyCategory.HosterUploads,
            scopeState,
            proxyRoutingCache
        );

        // Assert
        proxyServer.ShouldBeNull();
    }

    [Test]
    public void Select_ScopeOfSameGroupWithSpecificProxyServer_ReturnsThatProxyServer()
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
        var scopeState = new ProxyCategoryScopeState(
            ProxyCategory.HosterUploads,
            ProxySelection.SpecificProxyServer,
            ProxyServerId: 7
        );

        // Act
        var proxyServer = ProxyServerSelection.Select(
            ProxyCategory.HosterUploads,
            scopeState,
            proxyRoutingCache
        );

        // Assert
        proxyServer.ShouldBe(specificProxyServer);
    }
}
