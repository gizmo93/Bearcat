using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UnreadableSecrets;
using Bearcat.Domain.UseCases.ManageImageHosters;
using Bearcat.Domain.UseCases.ManageImageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageProxyServers.Selection;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageImageHosters;

public class ImageHosterServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string ImageHosterClassName = "TestImageHoster";
    private const string SerializedConfig = "{\"apiKey\":\"secret\"}";

    private Mock<IImageHoster> imageHosterMock = null!;
    private Mock<IImageHosterConfig> imageHosterConfigMock = null!;
    private Mock<IImageHosterFactory> imageHosterFactoryMock = null!;
    private ImageHosterService service = null!;

    [SetUp]
    public void Setup()
    {
        imageHosterConfigMock = new Mock<IImageHosterConfig>(MockBehavior.Strict);
        imageHosterMock = new Mock<IImageHoster>(MockBehavior.Strict);
        imageHosterMock.As<ISupportsLogin>();
        imageHosterFactoryMock = new Mock<IImageHosterFactory>(MockBehavior.Strict);

        imageHosterFactoryMock
            .Setup(factory => factory.Get(ImageHosterClassName))
            .Returns(imageHosterMock.Object);

        service = new ImageHosterService(
            new ImageHosterRegistrationWriteRepository(DbContext),
            new ImageHosterRegistrationReadRepository(DbContext, imageHosterFactoryMock.Object),
            imageHosterFactoryMock.Object,
            NoOpSecretProtector.Instance,
            UnreadableSecretsNotificationServiceFactory.Create(
                DbContext,
                CreateNotificationConfigurationProvider()
            ),
            new ProxySelectionValidator(new ProxyServerRepository(DbContext, DbContext))
        );
    }

    [Test]
    public async Task CreateAsync_ValidImageHoster_PersistsActiveRegistration()
    {
        // Arrange
        var configuration = new Dictionary<string, string> { ["apiKey"] = "secret" };
        imageHosterMock
            .Setup(hoster => hoster.SerializeConfig(configuration))
            .Returns(SerializedConfig);

        // Act
        await service.CreateAsync(
            "Primary image hoster",
            ImageHosterClassName,
            configuration,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var registration = await DbContext.ImageHosterRegistrations.SingleAsync();

        registration.Name.ShouldBe("Primary image hoster");
        registration.ImageHosterClassName.ShouldBe(ImageHosterClassName);
        registration.SerializedConfig.ShouldBe(SerializedConfig);
        registration.IsActive.ShouldBeTrue();
    }

    [Test]
    public async Task UpdateAsync_RegistrationExists_UpdatesNameAndSerializedConfig()
    {
        // Arrange
        var registration = await AddImageHosterRegistrationAsync(isActive: true);
        var configuration = new Dictionary<string, string> { ["apiKey"] = "updated" };
        imageHosterMock
            .Setup(hoster => hoster.DeserializeConfig(SerializedConfig))
            .Returns(imageHosterConfigMock.Object);
        imageHosterConfigMock
            .Setup(config => config.ToDictionary())
            .Returns(new Dictionary<string, string> { ["apiKey"] = "secret" });
        imageHosterMock
            .Setup(hoster =>
                hoster.SerializeConfig(
                    It.Is<IReadOnlyDictionary<string, string>>(config =>
                        config["apiKey"] == "updated"
                    )
                )
            )
            .Returns("{\"apiKey\":\"updated\"}");

        // Act
        await service.UpdateAsync(
            registration.Id,
            "Updated image hoster",
            configuration,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var result = await DbContext.ImageHosterRegistrations.SingleAsync();

        result.Name.ShouldBe("Updated image hoster");
        result.SerializedConfig.ShouldBe("{\"apiKey\":\"updated\"}");
        result.ImageHosterClassName.ShouldBe(ImageHosterClassName);
    }

    [Test]
    public async Task UpdateAsync_RegistrationHasUnreadableSecrets_ReplacesConfigAndClearsFlag()
    {
        // Arrange
        var registration = await AddImageHosterRegistrationAsync(
            isActive: true,
            hasUnreadableSecrets: true
        );
        imageHosterMock
            .Setup(hoster =>
                hoster.SerializeConfig(
                    It.Is<IReadOnlyDictionary<string, string>>(config =>
                        config.Count == 1 && config["apiKey"] == "updated"
                    )
                )
            )
            .Returns("{\"apiKey\":\"updated\"}");

        // Act
        await service.UpdateAsync(
            registration.Id,
            "Updated image hoster",
            new Dictionary<string, string> { ["apiKey"] = "updated" },
            cancellationToken: CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.ImageHosterRegistrations.SingleAsync();

        result.SerializedConfig.ShouldBe("{\"apiKey\":\"updated\"}");
        result.HasUnreadableSecrets.ShouldBeFalse();
        imageHosterMock.Verify(hoster => hoster.DeserializeConfig(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ToggleIsActiveAsync_RegistrationExists_TogglesIsActive()
    {
        // Arrange
        var registration = await AddImageHosterRegistrationAsync(isActive: true);

        // Act
        await service.ToggleIsActiveAsync(registration.Id, CancellationToken.None);

        // Assert
        var result = await DbContext.ImageHosterRegistrations.SingleAsync();

        result.IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task TryLoginAsync_RegistrationExists_DelegatesToImageHoster()
    {
        // Arrange
        var registration = await AddImageHosterRegistrationAsync(isActive: true);
        var loginResult = new TryLoginResult(true);
        imageHosterMock
            .Setup(hoster => hoster.DeserializeConfig(SerializedConfig))
            .Returns(imageHosterConfigMock.Object);
        imageHosterMock
            .As<ISupportsLogin>()
            .Setup(supportsLogin =>
                supportsLogin.TryLoginAsync(imageHosterConfigMock.Object, CancellationToken.None)
            )
            .ReturnsAsync(loginResult);

        // Act
        var result = await service.TryLoginAsync(registration.Id, CancellationToken.None);

        // Assert
        result.ShouldBe(loginResult);
    }

    [Test]
    public async Task DeleteAsync_RegistrationExists_RemovesRegistration()
    {
        // Arrange
        var registration = await AddImageHosterRegistrationAsync(isActive: true);

        // Act
        await service.DeleteAsync(registration.Id, CancellationToken.None);

        // Assert
        var result = await DbContext.ImageHosterRegistrations.AnyAsync();

        result.ShouldBeFalse();
    }

    [Test]
    public async Task GetImageUploadCountAsync_ImageUploadsOfSeveralRegistrations_CountsOnlyImageUploadsOfRegistration()
    {
        // Arrange
        var registration = await AddImageHosterRegistrationAsync(isActive: true);
        var otherRegistration = await AddImageHosterRegistrationAsync(isActive: true);
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
        var releaseCollection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = "collection",
            Name = "Collection",
            CreatedAt = DateTime.UtcNow,
        };
        DbContext.ImageUploadConfigs.AddRange(
            new ImageUploadConfig
            {
                Release = release,
                ImageHosterRegistration = registration,
                Name = "Cover",
                ImageUploads = [CreateImageUpload(), CreateImageUpload()],
            },
            new ImageUploadConfig
            {
                ReleaseCollection = releaseCollection,
                ImageHosterRegistration = registration,
                Name = "Collection cover",
                ImageUploads = [CreateImageUpload()],
            },
            new ImageUploadConfig
            {
                Release = release,
                ImageHosterRegistration = otherRegistration,
                Name = "Other cover",
                ImageUploads = [CreateImageUpload()],
            }
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await service.GetImageUploadCountAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(3);
    }

    [Test]
    public async Task GetImageUploadCountAsync_RegistrationWithoutImageUploads_ReturnsZero()
    {
        // Arrange
        var registration = await AddImageHosterRegistrationAsync(isActive: true);

        // Act
        var result = await service.GetImageUploadCountAsync(
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
        imageHosterMock
            .Setup(x => x.SerializeConfig(It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns(SerializedConfig);

        // Act
        await service.CreateAsync(
            "Primary image hoster",
            ImageHosterClassName,
            new Dictionary<string, string> { ["apiKey"] = "secret" },
            proxySelection: ProxySelection.SpecificProxyServer,
            proxyServerId: proxyServer.Id,
            cancellationToken: CancellationToken.None
        );

        // Assert
        var registration = await DbContext.ImageHosterRegistrations.SingleAsync();
        registration.ProxySelection.ShouldBe(ProxySelection.SpecificProxyServer);
        registration.ProxyServerId.ShouldBe(proxyServer.Id);
    }

    [Test]
    public async Task CreateAsync_SpecificProxyServerDoesNotExist_ThrowsAndStoresNothing()
    {
        // Arrange
        imageHosterMock
            .Setup(x => x.SerializeConfig(It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns(SerializedConfig);

        // Act
        var act = () =>
            service.CreateAsync(
                "Primary image hoster",
                ImageHosterClassName,
                new Dictionary<string, string> { ["apiKey"] = "secret" },
                proxySelection: ProxySelection.SpecificProxyServer,
                proxyServerId: 4711,
                cancellationToken: CancellationToken.None
            );

        // Assert
        await act.ShouldThrowAsync<InvalidProxySelectionException>();
        (await DbContext.ImageHosterRegistrations.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task CreateAsync_SpecificProxyServerWithoutProxyServerId_Throws()
    {
        // Arrange
        imageHosterMock
            .Setup(x => x.SerializeConfig(It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns(SerializedConfig);

        // Act
        var act = () =>
            service.CreateAsync(
                "Primary image hoster",
                ImageHosterClassName,
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
        var registration = await AddImageHosterRegistrationAsync(
            isActive: true,
            proxySelection: ProxySelection.SpecificProxyServer,
            proxyServerId: proxyServer.Id
        );
        imageHosterMock
            .Setup(x => x.DeserializeConfig(SerializedConfig))
            .Returns(imageHosterConfigMock.Object);
        imageHosterConfigMock
            .Setup(c => c.ToDictionary())
            .Returns(new Dictionary<string, string> { ["apiKey"] = "secret" });
        imageHosterMock
            .Setup(x => x.SerializeConfig(It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Returns(SerializedConfig);

        // Act
        await service.UpdateAsync(
            registration.Id,
            "Primary image hoster",
            new Dictionary<string, string>(),
            proxySelection: ProxySelection.UseCategoryDefault,
            proxyServerId: proxyServer.Id,
            cancellationToken: CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.ImageHosterRegistrations.SingleAsync();
        result.ProxySelection.ShouldBe(ProxySelection.UseCategoryDefault);
        result.ProxyServerId.ShouldBeNull();
    }

    [Test]
    public async Task TryLoginAsync_RegistrationWithProxySelection_CallsImageHosterInsideProxyScope()
    {
        // Arrange
        var proxyServer = await AddProxyServerAsync();
        var registration = await AddImageHosterRegistrationAsync(
            isActive: true,
            proxySelection: ProxySelection.SpecificProxyServer,
            proxyServerId: proxyServer.Id
        );
        ProxyCategoryScopeState? scopeStateDuringLogin = null;
        imageHosterMock
            .Setup(x => x.DeserializeConfig(SerializedConfig))
            .Returns(imageHosterConfigMock.Object);
        imageHosterMock
            .As<ISupportsLogin>()
            .Setup(supportsLogin =>
                supportsLogin.TryLoginAsync(imageHosterConfigMock.Object, CancellationToken.None)
            )
            .Callback(() => scopeStateDuringLogin = ProxyCategoryScope.Current)
            .ReturnsAsync(new TryLoginResult(true));

        // Act
        await service.TryLoginAsync(registration.Id, CancellationToken.None);

        // Assert
        scopeStateDuringLogin.ShouldBe(
            new ProxyCategoryScopeState(
                ProxyCategory.ImageHosters,
                ProxySelection.SpecificProxyServer,
                proxyServer.Id
            )
        );
        ProxyCategoryScope.Current.ShouldBeNull();
    }

    [Test]
    public async Task GetAllAsync_SeveralRegistrations_ReturnsReadModelsOrderedByName()
    {
        // Arrange
        var proxyServer = await AddProxyServerAsync();
        var secondaryRegistration = await AddImageHosterRegistrationAsync(
            isActive: false,
            hasUnreadableSecrets: true,
            proxySelection: ProxySelection.SpecificProxyServer,
            proxyServerId: proxyServer.Id,
            name: "Secondary image hoster"
        );
        var primaryRegistration = await AddImageHosterRegistrationAsync(isActive: true);
        DbContext.ChangeTracker.Clear();
        var readRepository = CreateReadRepositoryWithImageHosterLookup();

        // Act
        var result = await readRepository.GetAllAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([
            new ImageHosterRegistrationReadModel(
                primaryRegistration.Id,
                "Primary image hoster",
                ImageHosterClassName,
                "Test image hoster",
                IsActive: true,
                HasUnreadableSecrets: false,
                ProxySelection.UseCategoryDefault,
                null
            ),
            new ImageHosterRegistrationReadModel(
                secondaryRegistration.Id,
                "Secondary image hoster",
                ImageHosterClassName,
                "Test image hoster",
                IsActive: false,
                HasUnreadableSecrets: true,
                ProxySelection.SpecificProxyServer,
                proxyServer.Id
            ),
        ]);
    }

    [Test]
    public async Task GetByIdAsync_RegistrationExists_ReturnsReadModel()
    {
        // Arrange
        await AddImageHosterRegistrationAsync(isActive: true);
        var proxyServer = await AddProxyServerAsync();
        var registration = await AddImageHosterRegistrationAsync(
            isActive: false,
            proxySelection: ProxySelection.SpecificProxyServer,
            proxyServerId: proxyServer.Id,
            name: "Secondary image hoster"
        );
        DbContext.ChangeTracker.Clear();
        var readRepository = CreateReadRepositoryWithImageHosterLookup();

        // Act
        var result = await readRepository.GetByIdAsync(registration.Id, CancellationToken.None);

        // Assert
        result.ShouldBe(
            new ImageHosterRegistrationReadModel(
                registration.Id,
                "Secondary image hoster",
                ImageHosterClassName,
                "Test image hoster",
                IsActive: false,
                HasUnreadableSecrets: false,
                ProxySelection.SpecificProxyServer,
                proxyServer.Id
            )
        );
    }

    [Test]
    public async Task GetByIdAsync_RegistrationDoesNotExist_ReturnsNull()
    {
        // Arrange
        var registration = await AddImageHosterRegistrationAsync(isActive: true);
        var readRepository = CreateReadRepositoryWithImageHosterLookup();

        // Act
        var result = await readRepository.GetByIdAsync(registration.Id + 1, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    private ImageHosterRegistrationReadRepository CreateReadRepositoryWithImageHosterLookup()
    {
        imageHosterMock.Setup(hoster => hoster.Name).Returns("Test image hoster");
        imageHosterFactoryMock
            .Setup(factory => factory.GetByClassName())
            .Returns(
                new Dictionary<string, IImageHoster>
                {
                    [ImageHosterClassName] = imageHosterMock.Object,
                }
            );

        return new ImageHosterRegistrationReadRepository(DbContext, imageHosterFactoryMock.Object);
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

        DbContext.ProxyServers.Add(proxyServer);
        await DbContext.SaveChangesAsync();

        return proxyServer;
    }

    private async Task<ImageHosterRegistration> AddImageHosterRegistrationAsync(
        bool isActive,
        bool hasUnreadableSecrets = false,
        ProxySelection proxySelection = ProxySelection.UseCategoryDefault,
        int? proxyServerId = null,
        string name = "Primary image hoster"
    )
    {
        var registration = new ImageHosterRegistration
        {
            ProxySelection = proxySelection,
            ProxyServerId = proxyServerId,
            Name = name,
            IsActive = isActive,
            HasUnreadableSecrets = hasUnreadableSecrets,
            ImageHosterClassName = ImageHosterClassName,
            SerializedConfig = SerializedConfig,
        };

        DbContext.ImageHosterRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();

        return registration;
    }

    private static ImageUpload CreateImageUpload()
    {
        return new ImageUpload
        {
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            ImageUrls = [],
            ErrorMessages = [],
        };
    }
}
