using System.Net;
using Bearcat.Abstractions.Proxies;
using Shouldly;

namespace Bearcat.Abstractions.UnitTest.Proxies;

public class CategoryProxySelectingWebProxyTest
{
    private static readonly Uri Destination = new("https://api.hoster.test/upload");

    private FakeProxyRoutingCache proxyRoutingCache = null!;
    private CategoryProxySelectingWebProxy webProxy = null!;

    [SetUp]
    public void SetUp()
    {
        proxyRoutingCache = new FakeProxyRoutingCache();
        webProxy = new CategoryProxySelectingWebProxy(
            ProxyCategory.HosterUploads,
            proxyRoutingCache
        );
    }

    [Test]
    public void GetProxy_NoScope_ReturnsProxyOfClientCategory()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );

        // Act
        var proxyUri = webProxy.GetProxy(Destination);
        var isBypassed = webProxy.IsBypassed(Destination);

        // Assert
        proxyUri.ShouldBe(new Uri("http://upload-proxy.test:8080"));
        isBypassed.ShouldBeFalse();
    }

    [Test]
    public void IsBypassed_ClientCategoryWithoutProxy_ReturnsTrue()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterMirrorDownloads,
            "socks5://download-proxy.test:1080"
        );

        // Act
        var isBypassed = webProxy.IsBypassed(Destination);
        var proxyUri = webProxy.GetProxy(Destination);

        // Assert
        isBypassed.ShouldBeTrue();
        proxyUri.ShouldBeNull();
    }

    [Test]
    public void GetProxy_InsideScope_ReturnsProxyOfScopeCategory()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterMirrorDownloads,
            "socks5://download-proxy.test:1080"
        );

        // Act
        Uri? proxyUri;
        using (ProxyCategoryScope.Enter(ProxyCategory.HosterMirrorDownloads))
        {
            proxyUri = webProxy.GetProxy(Destination);
        }

        // Assert
        proxyUri.ShouldBe(new Uri("socks5://download-proxy.test:1080"));
    }

    [Test]
    public void IsBypassed_ScopeCategoryWithoutProxy_ReturnsTrueAlthoughClientCategoryHasProxy()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );

        // Act
        bool isBypassed;
        using (ProxyCategoryScope.Enter(ProxyCategory.HosterMirrorDownloads))
        {
            isBypassed = webProxy.IsBypassed(Destination);
        }

        // Assert
        isBypassed.ShouldBeTrue();
    }

    [Test]
    public void GetProxy_AfterScopeIsDisposed_ReturnsProxyOfClientCategoryAgain()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterMirrorDownloads,
            "socks5://download-proxy.test:1080"
        );
        ProxyCategoryScope.Enter(ProxyCategory.HosterMirrorDownloads).Dispose();

        // Act
        var proxyUri = webProxy.GetProxy(Destination);

        // Assert
        proxyUri.ShouldBe(new Uri("http://upload-proxy.test:8080"));
        ProxyCategoryScope.Current.ShouldBeNull();
    }

    [Test]
    public void GetProxy_NestedScopeIsDisposed_RestoresOuterScope()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(ProxyCategory.ImageHosters, "http://image-proxy.test:8080");
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterMirrorDownloads,
            "socks5://download-proxy.test:1080"
        );

        // Act
        Uri? proxyUri;
        using (ProxyCategoryScope.Enter(ProxyCategory.HosterMirrorDownloads))
        {
            ProxyCategoryScope.Enter(ProxyCategory.ImageHosters).Dispose();
            proxyUri = webProxy.GetProxy(Destination);
        }

        // Assert
        proxyUri.ShouldBe(new Uri("socks5://download-proxy.test:1080"));
    }

    [Test]
    public async Task GetProxy_ScopeEnteredBeforeParallelWork_FlowsIntoTasks()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterMirrorDownloads,
            "socks5://download-proxy.test:1080"
        );

        // Act
        Uri?[] proxyUris;
        using (ProxyCategoryScope.Enter(ProxyCategory.HosterMirrorDownloads))
        {
            proxyUris = await Task.WhenAll(
                Enumerable.Range(0, 3).Select(_ => Task.Run(() => webProxy.GetProxy(Destination)))
            );
        }

        // Assert
        proxyUris.ShouldAllBe(proxyUri => proxyUri == new Uri("socks5://download-proxy.test:1080"));
    }

    [Test]
    public void GetProxy_ProxyServerWithUnreadableSecrets_StillReturnsProxyInsteadOfDirectConnection()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080",
            hasUnreadableSecrets: true
        );

        // Act
        var isBypassed = webProxy.IsBypassed(Destination);
        var proxyUri = webProxy.GetProxy(Destination);

        // Assert
        isBypassed.ShouldBeFalse();
        proxyUri.ShouldBe(new Uri("http://upload-proxy.test:8080"));
    }

    [Test]
    public void Enter_OnlyCategory_UsesCategoryDefaultSelection()
    {
        // Act
        ProxyCategoryScopeState? scopeState;
        using (ProxyCategoryScope.Enter(ProxyCategory.HosterMirrorDownloads))
        {
            scopeState = ProxyCategoryScope.Current;
        }

        // Assert
        scopeState.ShouldBe(
            new ProxyCategoryScopeState(
                ProxyCategory.HosterMirrorDownloads,
                ProxySelection.UseCategoryDefault,
                ProxyServerId: null
            )
        );
    }

    [Test]
    public void GetProxy_ScopeOfSameCategoryWithUseCategoryDefault_ReturnsProxyOfCategory()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );
        proxyRoutingCache.AddProxyServer(7, "http://specific-proxy.test:3128");

        // Act
        Uri? proxyUri;
        using (
            ProxyCategoryScope.Enter(
                ProxyCategory.HosterUploads,
                ProxySelection.UseCategoryDefault,
                proxyServerId: null
            )
        )
        {
            proxyUri = webProxy.GetProxy(Destination);
        }

        // Assert
        proxyUri.ShouldBe(new Uri("http://upload-proxy.test:8080"));
    }

    [Test]
    public void IsBypassed_ScopeOfSameGroupWithNoProxy_ReturnsTrueAlthoughCategoriesHaveProxies()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterMirrorDownloads,
            "socks5://download-proxy.test:1080"
        );

        // Act
        bool isBypassed;
        Uri? proxyUri;
        using (
            ProxyCategoryScope.Enter(
                ProxyCategory.HosterMirrorDownloads,
                ProxySelection.NoProxy,
                proxyServerId: null
            )
        )
        {
            isBypassed = webProxy.IsBypassed(Destination);
            proxyUri = webProxy.GetProxy(Destination);
        }

        // Assert
        isBypassed.ShouldBeTrue();
        proxyUri.ShouldBeNull();
    }

    [Test]
    public void GetProxy_ScopeOfSameCategoryWithSpecificProxyServer_ReturnsThatProxyServer()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );
        proxyRoutingCache.AddProxyServer(7, "http://specific-proxy.test:3128");

        // Act
        Uri? proxyUri;
        using (
            ProxyCategoryScope.Enter(
                ProxyCategory.HosterUploads,
                ProxySelection.SpecificProxyServer,
                proxyServerId: 7
            )
        )
        {
            proxyUri = webProxy.GetProxy(Destination);
        }

        // Assert
        proxyUri.ShouldBe(new Uri("http://specific-proxy.test:3128"));
    }

    [Test]
    public void GetProxy_ScopeOfSameGroupWithSpecificProxyServer_ReturnsThatProxyServer()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterMirrorDownloads,
            "socks5://download-proxy.test:1080"
        );
        proxyRoutingCache.AddProxyServer(9, "socks5://specific-proxy.test:1081");

        // Act
        Uri? proxyUri;
        using (
            ProxyCategoryScope.Enter(
                ProxyCategory.HosterMirrorDownloads,
                ProxySelection.SpecificProxyServer,
                proxyServerId: 9
            )
        )
        {
            proxyUri = webProxy.GetProxy(Destination);
        }

        // Assert
        proxyUri.ShouldBe(new Uri("socks5://specific-proxy.test:1081"));
    }

    [Test]
    public void GetProxy_ScopeOfOtherGroupWithSpecificProxyServer_ReturnsProxyOfClientCategory()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );
        proxyRoutingCache.AddProxyServer(7, "http://specific-proxy.test:3128");

        // Act
        Uri? proxyUri;
        using (
            ProxyCategoryScope.Enter(
                ProxyCategory.LinkCrypters,
                ProxySelection.SpecificProxyServer,
                proxyServerId: 7
            )
        )
        {
            proxyUri = webProxy.GetProxy(Destination);
        }

        // Assert
        proxyUri.ShouldBe(new Uri("http://upload-proxy.test:8080"));
    }

    [Test]
    public void GetProxy_ScopeOfOtherGroupWithNoProxy_ReturnsProxyOfClientCategory()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(
            ProxyCategory.HosterUploads,
            "http://upload-proxy.test:8080"
        );

        // Act
        Uri? proxyUri;
        using (
            ProxyCategoryScope.Enter(
                ProxyCategory.ImageHosters,
                ProxySelection.NoProxy,
                proxyServerId: null
            )
        )
        {
            proxyUri = webProxy.GetProxy(Destination);
        }

        // Assert
        proxyUri.ShouldBe(new Uri("http://upload-proxy.test:8080"));
    }

    [Test]
    public void IsBypassed_ScopeOfOtherGroupWithUseCategoryDefault_UsesClientCategoryInsteadOfScopeCategory()
    {
        // Arrange
        proxyRoutingCache.RouteCategory(ProxyCategory.NfoDatabases, "http://nfo-proxy.test:8080");

        // Act
        bool isBypassed;
        using (ProxyCategoryScope.Enter(ProxyCategory.NfoDatabases))
        {
            isBypassed = webProxy.IsBypassed(Destination);
        }

        // Assert
        isBypassed.ShouldBeTrue();
    }

    [Test]
    public void GetProxy_NestedScopeWithNoProxyIsDisposed_RestoresOuterSpecificProxyServer()
    {
        // Arrange
        proxyRoutingCache.AddProxyServer(7, "http://specific-proxy.test:3128");

        // Act
        Uri? proxyUriInsideInnerScope;
        Uri? proxyUriAfterInnerScope;
        using (
            ProxyCategoryScope.Enter(
                ProxyCategory.HosterUploads,
                ProxySelection.SpecificProxyServer,
                proxyServerId: 7
            )
        )
        {
            using (
                ProxyCategoryScope.Enter(
                    ProxyCategory.HosterUploads,
                    ProxySelection.NoProxy,
                    proxyServerId: null
                )
            )
            {
                proxyUriInsideInnerScope = webProxy.GetProxy(Destination);
            }

            proxyUriAfterInnerScope = webProxy.GetProxy(Destination);
        }

        // Assert
        proxyUriInsideInnerScope.ShouldBeNull();
        proxyUriAfterInnerScope.ShouldBe(new Uri("http://specific-proxy.test:3128"));
        ProxyCategoryScope.Current.ShouldBeNull();
    }

    [Test]
    public void Credentials_GetCredential_LooksUpCredentialByProxyAddress()
    {
        // Arrange
        var proxyUri = new Uri("socks5://download-proxy.test:1080");
        var credential = new NetworkCredential("alice", "secret");
        proxyRoutingCache.CredentialByProxyAddress[proxyUri] = credential;

        // Act
        var result = webProxy.Credentials!.GetCredential(proxyUri, "socks5");

        // Assert
        result.ShouldBeSameAs(credential);
    }

    [Test]
    public void Credentials_UnknownProxyAddress_ReturnsNull()
    {
        // Act
        var result = webProxy.Credentials!.GetCredential(
            new Uri("http://unknown-proxy.test:8080"),
            "Basic"
        );

        // Assert
        result.ShouldBeNull();
    }
}
