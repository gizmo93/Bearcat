using Bearcat.Abstractions.Proxies;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;
using Bearcat.Domain.UseCases.ManageProxyServers.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Proxies;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Bearcat.Infrastructure.UnitTest.Proxies;

public class ProxyRoutingCacheTest
{
    private const string ReadableEncryptedPassword = "protected:secret";
    private const string UnreadableEncryptedPassword = "protected-with-lost-key";

    private List<ProxyServerRoutingReadModel> proxyServers = null!;
    private List<ProxyCategoryDefaultReadModel> categoryDefaults = null!;
    private ServiceProvider serviceProvider = null!;
    private ProxyRoutingCache cache = null!;

    [SetUp]
    public void SetUp()
    {
        proxyServers = [];
        categoryDefaults = [];

        var proxyServerRepositoryMock = new Mock<IProxyServerReadRepository>();
        proxyServerRepositoryMock
            .Setup(repository => repository.GetAllForRoutingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => proxyServers.ToList());
        var categoryDefaultRepositoryMock = new Mock<IProxyCategoryDefaultReadRepository>();
        categoryDefaultRepositoryMock
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => categoryDefaults.ToList());

        var secretProtectorMock = new Mock<ISecretProtector>();
        secretProtectorMock
            .Setup(protector => protector.CanUnprotect(ReadableEncryptedPassword))
            .Returns(true);
        secretProtectorMock
            .Setup(protector => protector.Unprotect(ReadableEncryptedPassword))
            .Returns("secret");
        secretProtectorMock
            .Setup(protector => protector.CanUnprotect(UnreadableEncryptedPassword))
            .Returns(false);

        var services = new ServiceCollection();
        services.AddScoped(_ => proxyServerRepositoryMock.Object);
        services.AddScoped(_ => categoryDefaultRepositoryMock.Object);
        serviceProvider = services.BuildServiceProvider();

        cache = new ProxyRoutingCache(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            secretProtectorMock.Object
        );
    }

    [TearDown]
    public void TearDown()
    {
        serviceProvider.Dispose();
    }

    [Test]
    public void GetProxyServerForCategory_BeforeRefresh_ReturnsNull()
    {
        // Act
        var result = cache.GetProxyServerForCategory(ProxyCategory.HosterUploads);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task RefreshAsync_CategoryWithHttpProxy_ResolvesProxyUriAndCredential()
    {
        // Arrange
        AddProxyServer(
            1,
            ProxyType.Http,
            "proxy.example.com",
            8080,
            "alice",
            ReadableEncryptedPassword
        );
        categoryDefaults.Add(new ProxyCategoryDefaultReadModel(ProxyCategory.HosterUploads, 1));

        // Act
        await cache.RefreshAsync(CancellationToken.None);

        // Assert
        var proxyServer = cache
            .GetProxyServerForCategory(ProxyCategory.HosterUploads)
            .ShouldNotBeNull();
        proxyServer.ProxyUri.ShouldBe(new Uri("http://proxy.example.com:8080"));
        proxyServer.HasUnreadableSecrets.ShouldBeFalse();
        proxyServer.Credential.ShouldNotBeNull();
        proxyServer.Credential.UserName.ShouldBe("alice");
        proxyServer.Credential.Password.ShouldBe("secret");
    }

    [Test]
    public async Task RefreshAsync_Socks5ProxyWithIpv6Host_ResolvesBracketedSocks5Uri()
    {
        // Arrange
        AddProxyServer(1, ProxyType.Socks5, "::1", 1080);
        categoryDefaults.Add(new ProxyCategoryDefaultReadModel(ProxyCategory.ImageHosters, 1));

        // Act
        await cache.RefreshAsync(CancellationToken.None);

        // Assert
        var proxyServer = cache
            .GetProxyServerForCategory(ProxyCategory.ImageHosters)
            .ShouldNotBeNull();
        proxyServer.ProxyUri.ShouldBe(new Uri("socks5://[::1]:1080"));
        proxyServer.Credential.ShouldBeNull();
    }

    [Test]
    public async Task RefreshAsync_CategoryDefaultWithoutProxyServer_UsesDirectConnection()
    {
        // Arrange
        AddProxyServer(1, ProxyType.Http, "proxy.example.com", 8080);
        categoryDefaults.Add(
            new ProxyCategoryDefaultReadModel(ProxyCategory.LinkCrypters, ProxyServerId: null)
        );

        // Act
        await cache.RefreshAsync(CancellationToken.None);

        // Assert
        cache.GetProxyServerForCategory(ProxyCategory.LinkCrypters).ShouldBeNull();
        cache.GetProxyServerForCategory(ProxyCategory.NfoDatabases).ShouldBeNull();
    }

    [Test]
    public async Task RefreshAsync_PasswordCannotBeDecrypted_MarksProxyServerAsUnreadableInsteadOfDirectConnection()
    {
        // Arrange
        AddProxyServer(
            1,
            ProxyType.Socks5,
            "proxy.example.com",
            1080,
            "alice",
            UnreadableEncryptedPassword
        );
        categoryDefaults.Add(new ProxyCategoryDefaultReadModel(ProxyCategory.HosterUploads, 1));

        // Act
        await cache.RefreshAsync(CancellationToken.None);

        // Assert
        var proxyServer = cache
            .GetProxyServerForCategory(ProxyCategory.HosterUploads)
            .ShouldNotBeNull();
        proxyServer.HasUnreadableSecrets.ShouldBeTrue();
        proxyServer.Credential.ShouldBeNull();
    }

    [Test]
    public async Task RefreshAsync_ProxyServerFlaggedWithUnreadableSecrets_MarksProxyServerAsUnreadable()
    {
        // Arrange
        AddProxyServer(
            1,
            ProxyType.Http,
            "proxy.example.com",
            8080,
            "alice",
            ReadableEncryptedPassword,
            hasUnreadableSecrets: true
        );
        categoryDefaults.Add(new ProxyCategoryDefaultReadModel(ProxyCategory.HosterUploads, 1));

        // Act
        await cache.RefreshAsync(CancellationToken.None);

        // Assert
        cache
            .GetProxyServerForCategory(ProxyCategory.HosterUploads)
            .ShouldNotBeNull()
            .HasUnreadableSecrets.ShouldBeTrue();
    }

    [Test]
    public async Task RefreshAsync_CategoryDefaultRemoved_DropsPreviousRouting()
    {
        // Arrange
        AddProxyServer(1, ProxyType.Http, "proxy.example.com", 8080);
        categoryDefaults.Add(new ProxyCategoryDefaultReadModel(ProxyCategory.HosterUploads, 1));
        await cache.RefreshAsync(CancellationToken.None);
        categoryDefaults.Clear();

        // Act
        await cache.RefreshAsync(CancellationToken.None);

        // Assert
        cache.GetProxyServerForCategory(ProxyCategory.HosterUploads).ShouldBeNull();
    }

    [TestCase("socks5://proxy.example.com:1080")]
    [TestCase("socks5://PROXY.example.com:1080/")]
    public async Task GetCredentialForProxyAddress_MatchingSchemeHostAndPort_ReturnsCredential(
        string proxyUri
    )
    {
        // Arrange
        AddProxyServer(
            1,
            ProxyType.Socks5,
            "proxy.example.com",
            1080,
            "alice",
            ReadableEncryptedPassword
        );
        await cache.RefreshAsync(CancellationToken.None);

        // Act
        var credential = cache.GetCredentialForProxyAddress(new Uri(proxyUri));

        // Assert
        credential.ShouldNotBeNull();
        credential.UserName.ShouldBe("alice");
        credential.Password.ShouldBe("secret");
    }

    [TestCase("http://proxy.example.com:1080")]
    [TestCase("socks5://proxy.example.com:1081")]
    [TestCase("socks5://other.example.com:1080")]
    public async Task GetCredentialForProxyAddress_DifferentSchemeHostOrPort_ReturnsNull(
        string proxyUri
    )
    {
        // Arrange
        AddProxyServer(
            1,
            ProxyType.Socks5,
            "proxy.example.com",
            1080,
            "alice",
            ReadableEncryptedPassword
        );
        await cache.RefreshAsync(CancellationToken.None);

        // Act
        var credential = cache.GetCredentialForProxyAddress(new Uri(proxyUri));

        // Assert
        credential.ShouldBeNull();
    }

    [Test]
    public async Task GetCredentialForProxyAddress_ProxyServerWithUnreadablePassword_ReturnsNull()
    {
        // Arrange
        AddProxyServer(
            1,
            ProxyType.Http,
            "proxy.example.com",
            8080,
            "alice",
            UnreadableEncryptedPassword
        );
        await cache.RefreshAsync(CancellationToken.None);

        // Act
        var credential = cache.GetCredentialForProxyAddress(
            new Uri("http://proxy.example.com:8080")
        );

        // Assert
        credential.ShouldBeNull();
    }

    private void AddProxyServer(
        int id,
        ProxyType proxyType,
        string host,
        int port,
        string? username = null,
        string? encryptedPassword = null,
        bool hasUnreadableSecrets = false
    )
    {
        proxyServers.Add(
            new ProxyServerRoutingReadModel(
                id,
                $"Proxy {id}",
                proxyType,
                host,
                port,
                username,
                encryptedPassword,
                hasUnreadableSecrets
            )
        );
    }
}
