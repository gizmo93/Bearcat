using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UnreadableSecrets;
using Bearcat.Domain.UseCases.ManageLinkCrypters;
using Bearcat.Domain.UseCases.ManageProxyServers.Selection;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageLinkCrypters;

public class LinkCrypterServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string LinkCrypterClassName = "TestCrypter";
    private const string SerializedConfig = "{\"apiKey\":\"secret\"}";

    private BearcatDbContext dbContext = null!;
    private Mock<ILinkCrypter> linkCrypterMock = null!;
    private Mock<ILinkCrypterConfig> linkCrypterConfigMock = null!;
    private Mock<ILinkCrypterFactory> linkCrypterFactoryMock = null!;
    private LinkCrypterService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        linkCrypterConfigMock = new Mock<ILinkCrypterConfig>(MockBehavior.Strict);
        linkCrypterMock = new Mock<ILinkCrypter>(MockBehavior.Strict);
        linkCrypterFactoryMock = new Mock<ILinkCrypterFactory>(MockBehavior.Strict);

        linkCrypterFactoryMock
            .Setup(f => f.Get(LinkCrypterClassName))
            .Returns(linkCrypterMock.Object);

        service = new LinkCrypterService(
            new LinkCrypterRegistrationWriteRepository(dbContext),
            new LinkCrypterRegistrationReadRepository(dbContext, linkCrypterFactoryMock.Object),
            linkCrypterFactoryMock.Object,
            NoOpSecretProtector.Instance,
            UnreadableSecretsNotificationServiceFactory.Create(
                dbContext,
                CreateNotificationConfigurationProvider()
            ),
            new ProxySelectionValidator(new ProxyServerRepository(dbContext, dbContext))
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_ValidLinkCrypter_PersistsActiveRegistration()
    {
        // Arrange
        var configuration = new Dictionary<string, string> { ["apiKey"] = "secret" };
        linkCrypterMock.Setup(c => c.SerializeConfig(configuration)).Returns(SerializedConfig);

        // Act
        await service.CreateAsync(
            "Primary crypter",
            LinkCrypterClassName,
            configuration,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var registration = await dbContext.LinkCrypterRegistrations.SingleAsync();

        registration.ShouldNotBeNull();
        registration.Name.ShouldBe("Primary crypter");
        registration.LinkCrypterClassName.ShouldBe(LinkCrypterClassName);
        registration.SerializedConfig.ShouldBe(SerializedConfig);
        registration.IsActive.ShouldBeTrue();
        linkCrypterFactoryMock.Verify(f => f.Get(LinkCrypterClassName), Times.Once);
        linkCrypterMock.Verify(c => c.SerializeConfig(configuration), Times.Once);
    }

    [Test]
    public async Task ToggleIsActiveAsync_RegistrationExists_TogglesIsActive()
    {
        // Arrange
        var registration = await AddLinkCrypterRegistrationAsync(isActive: true);

        // Act
        await service.ToggleIsActiveAsync(registration.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.LinkCrypterRegistrations.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(registration.Id);
        result.IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task UpdateAsync_RegistrationExists_UpdatesNameAndSerializedConfig()
    {
        // Arrange
        var registration = await AddLinkCrypterRegistrationAsync(isActive: true);
        var configuration = new Dictionary<string, string> { ["apiKey"] = "updated" };
        linkCrypterMock
            .Setup(c => c.DeserializeConfig(SerializedConfig))
            .Returns(linkCrypterConfigMock.Object);
        linkCrypterConfigMock
            .Setup(c => c.ToDictionary())
            .Returns(new Dictionary<string, string> { ["apiKey"] = "secret" });
        linkCrypterMock
            .Setup(c =>
                c.SerializeConfig(
                    It.Is<IReadOnlyDictionary<string, string>>(config =>
                        config["apiKey"] == "updated"
                    )
                )
            )
            .Returns("{\"apiKey\":\"updated\"}");

        // Act
        await service.UpdateAsync(
            registration.Id,
            "Updated crypter",
            configuration,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var result = await dbContext.LinkCrypterRegistrations.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(registration.Id);
        result.Name.ShouldBe("Updated crypter");
        result.SerializedConfig.ShouldBe("{\"apiKey\":\"updated\"}");
        result.LinkCrypterClassName.ShouldBe(LinkCrypterClassName);
        linkCrypterFactoryMock.Verify(f => f.Get(LinkCrypterClassName), Times.Once);
        linkCrypterMock.Verify(
            c =>
                c.SerializeConfig(
                    It.Is<IReadOnlyDictionary<string, string>>(config =>
                        config["apiKey"] == "updated"
                    )
                ),
            Times.Once
        );
    }

    [Test]
    public async Task UpdateAsync_RegistrationHasUnreadableSecrets_ReplacesConfigAndClearsFlag()
    {
        // Arrange
        var registration = await AddLinkCrypterRegistrationAsync(
            isActive: true,
            hasUnreadableSecrets: true
        );
        linkCrypterMock
            .Setup(c =>
                c.SerializeConfig(
                    It.Is<IReadOnlyDictionary<string, string>>(config =>
                        config.Count == 1 && config["apiKey"] == "updated"
                    )
                )
            )
            .Returns("{\"apiKey\":\"updated\"}");

        // Act
        await service.UpdateAsync(
            registration.Id,
            "Updated crypter",
            new Dictionary<string, string> { ["apiKey"] = "updated" },
            cancellationToken: CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.LinkCrypterRegistrations.SingleAsync();

        result.SerializedConfig.ShouldBe("{\"apiKey\":\"updated\"}");
        result.HasUnreadableSecrets.ShouldBeFalse();
        linkCrypterMock.Verify(c => c.DeserializeConfig(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task TryLoginAsync_RegistrationExists_DelegatesToLinkCrypter()
    {
        // Arrange
        var registration = await AddLinkCrypterRegistrationAsync(isActive: true);
        var loginResult = new TryLoginResult(true);
        linkCrypterMock
            .Setup(c => c.DeserializeConfig(SerializedConfig))
            .Returns(linkCrypterConfigMock.Object);
        linkCrypterMock
            .Setup(c => c.TryLoginAsync(linkCrypterConfigMock.Object, CancellationToken.None))
            .ReturnsAsync(loginResult);

        // Act
        var result = await service.TryLoginAsync(registration.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(loginResult);
        linkCrypterFactoryMock.Verify(f => f.Get(LinkCrypterClassName), Times.Once);
        linkCrypterMock.Verify(c => c.DeserializeConfig(SerializedConfig), Times.Once);
        linkCrypterMock.Verify(
            c => c.TryLoginAsync(linkCrypterConfigMock.Object, CancellationToken.None),
            Times.Once
        );
    }

    [Test]
    public async Task DeleteAsync_RegistrationExists_RemovesRegistration()
    {
        // Arrange
        var registration = await AddLinkCrypterRegistrationAsync(isActive: true);

        // Act
        await service.DeleteAsync(registration.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.LinkCrypterRegistrations.AnyAsync();

        result.ShouldBeFalse();
    }

    [Test]
    public async Task GetLinkCrypterContainerCountAsync_ContainersOfSeveralRegistrations_CountsOnlyContainersOfRegistration()
    {
        // Arrange
        var registration = await AddLinkCrypterRegistrationAsync(isActive: true);
        var otherRegistration = await AddLinkCrypterRegistrationAsync(isActive: true);
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var release = new Release
        {
            Name = "Bearcat.Release.001",
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/release",
            ReleaseGroup = releaseGroup,
        };
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = new ArchiveConfig
            {
                Release = release,
                Name = "Main archive",
                ArchiveFilesBasePath = "/tmp/archive",
                ArchiverName = "zip",
                ArchiveFileSizeMb = 512,
            },
            HosterRegistration = new HosterRegistration
            {
                Name = "Hoster",
                SerializedConfig = "{}",
                HosterClassName = "TestHoster",
                IsActive = true,
            },
            Name = "Default upload",
        };
        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles = [],
            ErrorMessages = [],
        };
        var collectionUploadSlot = new CollectionUploadSlot
        {
            ReleaseCollection = new ReleaseCollection
            {
                ReleaseGroup = releaseGroup,
                Key = "collection",
                Name = "Collection",
                CreatedAt = DateTime.UtcNow,
            },
            Key = "slot",
            Name = "Slot",
        };
        var releaseContainer = CreateLinkCrypterContainer(
            registration,
            LinkCrypterContainerScope.Release
        );
        releaseContainer.UploadConfigLinkCrypter = new UploadConfigLinkCrypter
        {
            UploadConfig = uploadConfig,
            LinkCrypterRegistration = registration,
        };
        releaseContainer.Upload = upload;
        var collectionContainer = CreateLinkCrypterContainer(
            registration,
            LinkCrypterContainerScope.ReleaseCollection
        );
        collectionContainer.CollectionUploadSlot = collectionUploadSlot;
        var otherReleaseContainer = CreateLinkCrypterContainer(
            otherRegistration,
            LinkCrypterContainerScope.Release
        );
        otherReleaseContainer.UploadConfigLinkCrypter = new UploadConfigLinkCrypter
        {
            UploadConfig = uploadConfig,
            LinkCrypterRegistration = otherRegistration,
        };
        otherReleaseContainer.Upload = upload;
        dbContext.LinkCrypterContainers.AddRange(
            releaseContainer,
            collectionContainer,
            otherReleaseContainer
        );
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetLinkCrypterContainerCountAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(2);
    }

    [Test]
    public async Task GetLinkCrypterContainerCountAsync_RegistrationWithoutContainers_ReturnsZero()
    {
        // Arrange
        var registration = await AddLinkCrypterRegistrationAsync(isActive: true);

        // Act
        var result = await service.GetLinkCrypterContainerCountAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
    }

    [Test]
    public async Task CreateAsync_SpecificProxyServer_StoresSelectionAndProxyServerId()
    {
        // Arrange
        var proxyServer = await AddProxyServerAsync();
        linkCrypterMock
            .Setup(x => x.SerializeConfig(It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns(SerializedConfig);

        // Act
        await service.CreateAsync(
            "Primary crypter",
            LinkCrypterClassName,
            new Dictionary<string, string> { ["apiKey"] = "secret" },
            proxySelection: ProxySelection.SpecificProxyServer,
            proxyServerId: proxyServer.Id,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var registration = await dbContext.LinkCrypterRegistrations.SingleAsync();
        registration.ProxySelection.ShouldBe(ProxySelection.SpecificProxyServer);
        registration.ProxyServerId.ShouldBe(proxyServer.Id);
    }

    [Test]
    public async Task CreateAsync_SpecificProxyServerDoesNotExist_ThrowsAndStoresNothing()
    {
        // Arrange
        linkCrypterMock
            .Setup(x => x.SerializeConfig(It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns(SerializedConfig);

        // Act
        var act = () =>
            service.CreateAsync(
                "Primary crypter",
                LinkCrypterClassName,
                new Dictionary<string, string> { ["apiKey"] = "secret" },
                proxySelection: ProxySelection.SpecificProxyServer,
                proxyServerId: 4711,
                cancellationToken: CancellationToken.None
            );

        // Assert
        await act.ShouldThrowAsync<InvalidProxySelectionException>();
        (await dbContext.LinkCrypterRegistrations.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task CreateAsync_SpecificProxyServerWithoutProxyServerId_Throws()
    {
        // Arrange
        linkCrypterMock
            .Setup(x => x.SerializeConfig(It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns(SerializedConfig);

        // Act
        var act = () =>
            service.CreateAsync(
                "Primary crypter",
                LinkCrypterClassName,
                new Dictionary<string, string> { ["apiKey"] = "secret" },
                proxySelection: ProxySelection.SpecificProxyServer,
                proxyServerId: null,
                cancellationToken: CancellationToken.None
            );

        // Assert
        await act.ShouldThrowAsync<InvalidProxySelectionException>();
    }

    [Test]
    public async Task UpdateAsync_UseCategoryDefaultWithProxyServerId_StoresCategoryDefaultWithoutProxyServerId()
    {
        // Arrange
        var proxyServer = await AddProxyServerAsync();
        var registration = await AddLinkCrypterRegistrationAsync(
            isActive: true,
            proxySelection: ProxySelection.SpecificProxyServer,
            proxyServerId: proxyServer.Id
        );
        linkCrypterMock
            .Setup(x => x.DeserializeConfig(SerializedConfig))
            .Returns(linkCrypterConfigMock.Object);
        linkCrypterConfigMock
            .Setup(c => c.ToDictionary())
            .Returns(new Dictionary<string, string> { ["apiKey"] = "secret" });
        linkCrypterMock
            .Setup(x => x.SerializeConfig(It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns(SerializedConfig);

        // Act
        await service.UpdateAsync(
            registration.Id,
            "Primary crypter",
            new Dictionary<string, string>(),
            proxySelection: ProxySelection.UseCategoryDefault,
            proxyServerId: proxyServer.Id,
            cancellationToken: CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.LinkCrypterRegistrations.SingleAsync();
        result.ProxySelection.ShouldBe(ProxySelection.UseCategoryDefault);
        result.ProxyServerId.ShouldBeNull();
    }

    [Test]
    public async Task TryLoginAsync_RegistrationWithProxySelection_CallsLinkCrypterInsideProxyScope()
    {
        // Arrange
        var proxyServer = await AddProxyServerAsync();
        var registration = await AddLinkCrypterRegistrationAsync(
            isActive: true,
            proxySelection: ProxySelection.SpecificProxyServer,
            proxyServerId: proxyServer.Id
        );
        ProxyCategoryScopeState? scopeStateDuringLogin = null;
        linkCrypterMock
            .Setup(x => x.DeserializeConfig(SerializedConfig))
            .Returns(linkCrypterConfigMock.Object);
        linkCrypterMock
            .Setup(c => c.TryLoginAsync(linkCrypterConfigMock.Object, CancellationToken.None))
            .Callback(() => scopeStateDuringLogin = ProxyCategoryScope.Current)
            .ReturnsAsync(new TryLoginResult(true));

        // Act
        await service.TryLoginAsync(registration.Id, CancellationToken.None);

        // Assert
        scopeStateDuringLogin.ShouldBe(
            new ProxyCategoryScopeState(
                ProxyCategory.LinkCrypters,
                ProxySelection.SpecificProxyServer,
                proxyServer.Id
            )
        );
        ProxyCategoryScope.Current.ShouldBeNull();
    }

    private async Task<ProxyServer> AddProxyServerAsync()
    {
        var proxyServer = new ProxyServer
        {
            Name = "Proxy",
            ProxyType = ProxyType.Http,
            Host = "proxy.example.com",
            Port = 8080,
        };

        dbContext.ProxyServers.Add(proxyServer);
        await dbContext.SaveChangesAsync();

        return proxyServer;
    }

    private async Task<LinkCrypterRegistration> AddLinkCrypterRegistrationAsync(
        bool isActive,
        bool hasUnreadableSecrets = false,
        ProxySelection proxySelection = ProxySelection.UseCategoryDefault,
        int? proxyServerId = null
    )
    {
        var registration = new LinkCrypterRegistration
        {
            ProxySelection = proxySelection,
            ProxyServerId = proxyServerId,
            Name = "Primary crypter",
            IsActive = isActive,
            HasUnreadableSecrets = hasUnreadableSecrets,
            LinkCrypterClassName = LinkCrypterClassName,
            SerializedConfig = SerializedConfig,
        };

        dbContext.LinkCrypterRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();

        return registration;
    }

    private static LinkCrypterContainer CreateLinkCrypterContainer(
        LinkCrypterRegistration registration,
        LinkCrypterContainerScope scope
    )
    {
        return new LinkCrypterContainer
        {
            Scope = scope,
            LinkCrypterRegistration = registration,
            ContainerUrl = "https://crypter.test/container",
            State = LinkCrypterContainerState.Created,
            Errors = [],
            CreatedAt = DateTime.UtcNow,
        };
    }
}
