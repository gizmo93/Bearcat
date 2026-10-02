using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageProxyServers;

public class ProxyCategoryDefaultRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    [Test]
    public async Task GetAllAsync_SeveralDefaults_ReturnsDefaultsOrderedByCategory()
    {
        // Arrange
        var proxyServer = await AddProxyServerAsync();
        var repository = CreateRepository();
        repository.Add(
            new ProxyCategoryDefault
            {
                ProxyCategory = ProxyCategory.MediaDatabases,
                ProxyServerId = null,
            }
        );
        repository.Add(
            new ProxyCategoryDefault
            {
                ProxyCategory = ProxyCategory.HosterUploads,
                ProxyServerId = proxyServer.Id,
            }
        );
        await repository.SaveChangesAsync(CancellationToken.None);

        // Act
        var result = await CreateRepository().GetAllAsync();

        // Assert
        result.ShouldBe([
            new ProxyCategoryDefaultReadModel(ProxyCategory.HosterUploads, proxyServer.Id),
            new ProxyCategoryDefaultReadModel(ProxyCategory.MediaDatabases, ProxyServerId: null),
        ]);
    }

    [Test]
    public async Task GetAllForUpdateAsync_ChangedProxyServer_PersistsChange()
    {
        // Arrange
        var proxyServer = await AddProxyServerAsync();
        var repository = CreateRepository();
        repository.Add(
            new ProxyCategoryDefault
            {
                ProxyCategory = ProxyCategory.NfoDatabases,
                ProxyServerId = proxyServer.Id,
            }
        );
        await repository.SaveChangesAsync(CancellationToken.None);
        var updateRepository = CreateRepository();

        // Act
        var storedDefault = (
            await updateRepository.GetAllForUpdateAsync(CancellationToken.None)
        ).ShouldHaveSingleItem();
        storedDefault.ProxyServerId = null;
        await updateRepository.SaveChangesAsync(CancellationToken.None);

        // Assert
        var persistedDefault = await CreateDbContext().ProxyCategoryDefaults.SingleAsync();
        persistedDefault.ProxyCategory.ShouldBe(ProxyCategory.NfoDatabases);
        persistedDefault.ProxyServerId.ShouldBeNull();
    }

    [Test]
    public async Task SaveChangesAsync_UnknownProxyServer_ThrowsDbUpdateException()
    {
        // Arrange
        var repository = CreateRepository();
        repository.Add(
            new ProxyCategoryDefault
            {
                ProxyCategory = ProxyCategory.ImageHosters,
                ProxyServerId = 4711,
            }
        );

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
    }

    private async Task<ProxyServer> AddProxyServerAsync()
    {
        var dbContext = CreateDbContext();
        var proxyServer = new ProxyServer
        {
            Name = "Upload proxy",
            ProxyType = ProxyType.Http,
            Host = "proxy.example.com",
            Port = 8080,
        };
        dbContext.Add(proxyServer);
        await dbContext.SaveChangesAsync();

        return proxyServer;
    }

    private ProxyCategoryDefaultRepository CreateRepository()
    {
        var dbContext = CreateDbContext();

        return new ProxyCategoryDefaultRepository(dbContext, dbContext);
    }
}
