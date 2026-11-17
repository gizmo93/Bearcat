using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ProxyServerRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
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
    public async Task GetUsageAsync_ProxyServerUsedByRegistrations_ReturnsRegistrationsOrderedByTypeAndName()
    {
        // Arrange
        var used = CreateProxyServer("Upload proxy");
        var other = CreateProxyServer("Download proxy", port: 1080);
        await AddProxyServersAsync(used, other);
        var dbContext = CreateDbContext();
        dbContext.AddRange(
            CreateHosterRegistration("Rapidgator", uploadProxyServerId: used.Id),
            CreateHosterRegistration("Alfafile", mirrorDownloadProxyServerId: used.Id),
            CreateHosterRegistration("Katfile", uploadProxyServerId: other.Id),
            CreateImageHosterRegistration("ImgBb", used.Id),
            CreateImageHosterRegistration("PixHost", other.Id),
            CreateLinkCrypterRegistration("FileCrypt", used.Id),
            CreateLinkCrypterRegistration("KeepLinks", null)
        );
        await dbContext.SaveChangesAsync();

        // Act
        var result = await CreateRepository().GetUsageAsync(used.Id, CancellationToken.None);

        // Assert
        result.CategoryDefaults.ShouldBeEmpty();
        result.Registrations.ShouldBe([
            new ProxyServerRegistrationUsageReadModel(
                ProxyUsingRegistrationType.Hoster,
                "Alfafile"
            ),
            new ProxyServerRegistrationUsageReadModel(
                ProxyUsingRegistrationType.Hoster,
                "Rapidgator"
            ),
            new ProxyServerRegistrationUsageReadModel(
                ProxyUsingRegistrationType.ImageHoster,
                "ImgBb"
            ),
            new ProxyServerRegistrationUsageReadModel(
                ProxyUsingRegistrationType.LinkCrypter,
                "FileCrypt"
            ),
        ]);
        result.IsUsed.ShouldBeTrue();
    }

    private static HosterRegistration CreateHosterRegistration(
        string name,
        int? uploadProxyServerId = null,
        int? mirrorDownloadProxyServerId = null
    ) =>
        new()
        {
            Name = name,
            HosterClassName = "TestHoster",
            SerializedConfig = "{}",
            UploadProxySelection = uploadProxyServerId is null
                ? ProxySelection.UseCategoryDefault
                : ProxySelection.SpecificProxyServer,
            UploadProxyServerId = uploadProxyServerId,
            MirrorDownloadProxySelection = mirrorDownloadProxyServerId is null
                ? ProxySelection.UseCategoryDefault
                : ProxySelection.SpecificProxyServer,
            MirrorDownloadProxyServerId = mirrorDownloadProxyServerId,
        };

    private static ImageHosterRegistration CreateImageHosterRegistration(
        string name,
        int? proxyServerId
    ) =>
        new()
        {
            Name = name,
            ImageHosterClassName = "TestImageHoster",
            SerializedConfig = "{}",
            ProxySelection = proxyServerId is null
                ? ProxySelection.UseCategoryDefault
                : ProxySelection.SpecificProxyServer,
            ProxyServerId = proxyServerId,
        };

    private static LinkCrypterRegistration CreateLinkCrypterRegistration(
        string name,
        int? proxyServerId
    ) =>
        new()
        {
            Name = name,
            LinkCrypterClassName = "TestCrypter",
            SerializedConfig = "{}",
            ProxySelection = proxyServerId is null
                ? ProxySelection.UseCategoryDefault
                : ProxySelection.SpecificProxyServer,
            ProxyServerId = proxyServerId,
        };

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
