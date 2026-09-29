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

    [Test]
    public async Task SaveChangesAsync_RemovedProxyServerIsUsedByRegistration_ThrowsDbUpdateException()
    {
        // Arrange
        var proxyServer = CreateProxyServer("Upload proxy");
        await AddProxyServersAsync(proxyServer);
        var dbContext = CreateDbContext();
        dbContext.Add(CreateLinkCrypterRegistration("FileCrypt", proxyServer.Id));
        await dbContext.SaveChangesAsync();
        var repository = CreateRepository();
        repository.Remove(await repository.GetByIdAsync(proxyServer.Id, CancellationToken.None));

        // Act
        var result = await Should.ThrowAsync<DbUpdateException>(() =>
            repository.SaveChangesAsync(CancellationToken.None)
        );

        // Assert
        result.ShouldNotBeNull();
    }

    [Test]
    public async Task ExistsAsync_ExistingProxyServer_ReturnsTrue()
    {
        // Arrange
        var proxyServer = CreateProxyServer("Upload proxy");
        await AddProxyServersAsync(proxyServer);

        // Act
        var result = await CreateRepository().ExistsAsync(proxyServer.Id, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task ExistsAsync_UnknownId_ReturnsFalse()
    {
        // Act
        var result = await CreateRepository().ExistsAsync(4711, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
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
