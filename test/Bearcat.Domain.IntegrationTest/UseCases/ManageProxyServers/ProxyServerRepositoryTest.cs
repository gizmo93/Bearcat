using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageProxyServers;

public class ProxyServerRepositoryTest : BearcatIntegrationTest
{
    [Test]
    public async Task GetAllAsync_SeveralProxyServers_ReturnsProxyServersOrderedByName()
    {
        // Arrange
        var withPassword = CreateProxyServer("Upload proxy", encryptedPassword: "encrypted");
        withPassword.HasUnreadableSecrets = true;
        var withoutCredentials = CreateProxyServer("Download proxy", username: null, port: 1080);
        withoutCredentials.ProxyType = ProxyType.Socks5;
        await AddProxyServersAsync(withPassword, withoutCredentials);

        // Act
        var result = await CreateRepository().GetAllAsync();

        // Assert
        result.ShouldBe([
            new ProxyServerReadModel(
                withoutCredentials.Id,
                "Download proxy",
                ProxyType.Socks5,
                "proxy.example.com",
                1080,
                Username: null,
                HasStoredPassword: false,
                HasUnreadableSecrets: false
            ),
            new ProxyServerReadModel(
                withPassword.Id,
                "Upload proxy",
                ProxyType.Http,
                "proxy.example.com",
                8080,
                "alice",
                HasStoredPassword: true,
                HasUnreadableSecrets: true
            ),
        ]);
    }

    [Test]
    public async Task GetReadModelAsync_ExistingProxyServer_ReturnsReadModel()
    {
        // Arrange
        var proxyServer = CreateProxyServer("Upload proxy", encryptedPassword: "encrypted");
        await AddProxyServersAsync(proxyServer);

        // Act
        var result = await CreateRepository().GetReadModelAsync(proxyServer.Id);

        // Assert
        result.ShouldBe(
            new ProxyServerReadModel(
                proxyServer.Id,
                "Upload proxy",
                ProxyType.Http,
                "proxy.example.com",
                8080,
                "alice",
                HasStoredPassword: true,
                HasUnreadableSecrets: false
            )
        );
    }

    [Test]
    public async Task GetReadModelAsync_UnknownId_ReturnsNull()
    {
        // Act
        var result = await CreateRepository().GetReadModelAsync(4711);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task NameExistsAsync_NameOfExistingProxyServer_ReturnsTrue()
    {
        // Arrange
        await AddProxyServersAsync(CreateProxyServer("Upload proxy"));

        // Act
        var result = await CreateRepository()
            .NameExistsAsync("Upload proxy", excludedProxyServerId: null, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task NameExistsAsync_NameOnlyUsedByExcludedProxyServer_ReturnsFalse()
    {
        // Arrange
        var proxyServer = CreateProxyServer("Upload proxy");
        await AddProxyServersAsync(proxyServer);

        // Act
        var result = await CreateRepository()
            .NameExistsAsync("Upload proxy", proxyServer.Id, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task NameExistsAsync_UnusedName_ReturnsFalse()
    {
        // Arrange
        await AddProxyServersAsync(CreateProxyServer("Upload proxy"));

        // Act
        var result = await CreateRepository()
            .NameExistsAsync("Download proxy", excludedProxyServerId: null, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task Remove_ExistingProxyServer_DeletesProxyServer()
    {
        // Arrange
        var proxyServer = CreateProxyServer("Upload proxy");
        await AddProxyServersAsync(proxyServer);
        var repository = CreateRepository();

        // Act
        repository.Remove(await repository.GetByIdAsync(proxyServer.Id, CancellationToken.None));
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        (await CreateDbContext().ProxyServers.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task SaveChangesAsync_DuplicateName_ThrowsDbUpdateException()
    {
        // Arrange
        await AddProxyServersAsync(CreateProxyServer("Upload proxy"));
        var repository = CreateRepository();
        repository.Add(CreateProxyServer("Upload proxy", port: 1080));

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
    }

    [Test]
    public async Task SaveChangesAsync_DuplicateHostAndPort_ThrowsDbUpdateException()
    {
        // Arrange
        await AddProxyServersAsync(CreateProxyServer("Upload proxy"));
        var repository = CreateRepository();
        repository.Add(CreateProxyServer("Download proxy"));

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
    }

    [TestCase("proxy.example.com", 8080, true)]
    [TestCase("PROXY.EXAMPLE.COM", 8080, true)]
    [TestCase("proxy.example.com", 3128, false)]
    [TestCase("other.example.com", 8080, false)]
    public async Task HostAndPortExistAsync_OtherProxyServer_ReturnsWhetherHostAndPortAreUsed(
        string host,
        int port,
        bool expectedResult
    )
    {
        // Arrange
        await AddProxyServersAsync(CreateProxyServer("Upload proxy"));

        // Act
        var result = await CreateRepository()
            .HostAndPortExistAsync(host, port, excludedProxyServerId: null, CancellationToken.None);

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Test]
    public async Task HostAndPortExistAsync_OnlyUsedByExcludedProxyServer_ReturnsFalse()
    {
        // Arrange
        var proxyServer = CreateProxyServer("Upload proxy");
        await AddProxyServersAsync(proxyServer);

        // Act
        var result = await CreateRepository()
            .HostAndPortExistAsync(
                "proxy.example.com",
                8080,
                proxyServer.Id,
                CancellationToken.None
            );

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public async Task GetAllForRoutingAsync_SeveralProxyServers_ReturnsEncryptedPasswordsOrderedById()
    {
        // Arrange
        var first = CreateProxyServer("Upload proxy", encryptedPassword: "encrypted");
        var second = CreateProxyServer("Download proxy", username: null, port: 1080);
        second.ProxyType = ProxyType.Socks5;
        second.HasUnreadableSecrets = true;
        await AddProxyServersAsync(first, second);

        // Act
        var result = await CreateRepository().GetAllForRoutingAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([
            new ProxyServerRoutingReadModel(
                first.Id,
                "Upload proxy",
                ProxyType.Http,
                "proxy.example.com",
                8080,
                "alice",
                "encrypted",
                HasUnreadableSecrets: false
            ),
            new ProxyServerRoutingReadModel(
                second.Id,
                "Download proxy",
                ProxyType.Socks5,
                "proxy.example.com",
                1080,
                Username: null,
                EncryptedPassword: null,
                HasUnreadableSecrets: true
            ),
        ]);
    }

    [Test]
    public async Task GetUsageAsync_ProxyServerUsedAsCategoryDefault_ReturnsCategoriesInOrder()
    {
        // Arrange
        var used = CreateProxyServer("Upload proxy");
        var other = CreateProxyServer("Download proxy", port: 1080);
        await AddProxyServersAsync(used, other);
        await AddCategoryDefaultsAsync(
            (ProxyCategory.ImageHosters, used.Id),
            (ProxyCategory.HosterUploads, used.Id),
            (ProxyCategory.HosterMirrorDownloads, other.Id),
            (ProxyCategory.LinkCrypters, null)
        );

        // Act
        var result = await CreateRepository().GetUsageAsync(used.Id, CancellationToken.None);

        // Assert
        result.CategoryDefaults.ShouldBe([ProxyCategory.HosterUploads, ProxyCategory.ImageHosters]);
        result.IsUsed.ShouldBeTrue();
    }

    [Test]
    public async Task GetUsageAsync_UnusedProxyServer_ReturnsNoCategories()
    {
        // Arrange
        var proxyServer = CreateProxyServer("Upload proxy");
        await AddProxyServersAsync(proxyServer);

        // Act
        var result = await CreateRepository().GetUsageAsync(proxyServer.Id, CancellationToken.None);

        // Assert
        result.IsUsed.ShouldBeFalse();
    }

    [Test]
    public async Task SaveChangesAsync_RemovedProxyServerIsCategoryDefault_ThrowsDbUpdateException()
    {
        // Arrange
        var proxyServer = CreateProxyServer("Upload proxy");
        await AddProxyServersAsync(proxyServer);
        await AddCategoryDefaultsAsync((ProxyCategory.HosterUploads, proxyServer.Id));
        var repository = CreateRepository();
        repository.Remove(await repository.GetByIdAsync(proxyServer.Id, CancellationToken.None));

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
    }

    private async Task AddCategoryDefaultsAsync(
        params (ProxyCategory ProxyCategory, int? ProxyServerId)[] categoryDefaults
    )
    {
        var dbContext = CreateDbContext();

        foreach (var (proxyCategory, proxyServerId) in categoryDefaults)
        {
            dbContext.Add(
                new ProxyCategoryDefault
                {
                    ProxyCategory = proxyCategory,
                    ProxyServerId = proxyServerId,
                }
            );
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task AddProxyServersAsync(params ProxyServer[] proxyServers)
    {
        var repository = CreateRepository();

        foreach (var proxyServer in proxyServers)
        {
            repository.Add(proxyServer);
        }

        await repository.SaveChangesAsync(CancellationToken.None);
    }

    private ProxyServerRepository CreateRepository()
    {
        var dbContext = CreateDbContext();

        return new ProxyServerRepository(dbContext, dbContext);
    }

    private static ProxyServer CreateProxyServer(
        string name,
        string? username = "alice",
        string? encryptedPassword = null,
        int port = 8080
    ) =>
        new()
        {
            Name = name,
            ProxyType = ProxyType.Http,
            Host = "proxy.example.com",
            Port = port,
            Username = username,
            EncryptedPassword = encryptedPassword,
        };
}
