using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UnreadableSecrets;
using Bearcat.Domain.UseCases.ManageMediaDatabases;
using Bearcat.Domain.UseCases.ManageMediaDatabases.ReadModels;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageMediaDatabases;

public class MediaDatabaseRegistrationServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string ClassName = "TvdbMetadataDatabase";
    private const string OtherClassName = "SteamMetadataDatabase";
    private const string SerializedConfig = "{\"ApiKey\":\"secret\"}";

    private Mock<IMediaMetadataDatabase> databaseMock = null!;
    private Mock<IMediaMetadataDatabaseConfig> configMock = null!;
    private Mock<IMediaMetadataDatabaseFactory> factoryMock = null!;
    private MediaDatabaseRegistrationService service = null!;

    [SetUp]
    public void Setup()
    {
        databaseMock = new Mock<IMediaMetadataDatabase>(MockBehavior.Strict);
        configMock = new Mock<IMediaMetadataDatabaseConfig>(MockBehavior.Strict);
        factoryMock = new Mock<IMediaMetadataDatabaseFactory>(MockBehavior.Strict);
        factoryMock.Setup(factory => factory.Get(ClassName)).Returns(databaseMock.Object);

        service = new MediaDatabaseRegistrationService(
            new MediaDatabaseRegistrationRepository(DbContext, DbContext, factoryMock.Object),
            factoryMock.Object,
            NoOpSecretProtector.Instance,
            UnreadableSecretsNotificationServiceFactory.Create(
                DbContext,
                CreateNotificationConfigurationProvider()
            )
        );
    }

    [Test]
    public async Task CreateAsync_ClassNameNotRegistered_PersistsRegistration()
    {
        // Arrange
        var configuration = new Dictionary<string, string> { ["ApiKey"] = "secret" };
        databaseMock
            .Setup(database =>
                database.SerializeConfig(
                    It.Is<IReadOnlyDictionary<string, string>>(config =>
                        config["ApiKey"] == "secret"
                    )
                )
            )
            .Returns(SerializedConfig);

        // Act
        await service.CreateAsync(ClassName, configuration, CancellationToken.None);

        // Assert
        var registration = await DbContext.MediaDatabaseRegistrations.SingleAsync();
        registration.MediaDatabaseClassName.ShouldBe(ClassName);
        registration.SerializedConfig.ShouldBe(SerializedConfig);
        registration.IsActive.ShouldBeTrue();
    }

    [Test]
    public async Task CreateAsync_ClassNameAlreadyRegistered_Throws()
    {
        // Arrange
        await AddRegistrationAsync();

        // Act / Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            service.CreateAsync(ClassName, new Dictionary<string, string>(), CancellationToken.None)
        );
        exception.Message.ShouldContain(ClassName);
    }

    [Test]
    public async Task UpdateAsync_RegistrationExists_MergesConfigurationAndPersists()
    {
        // Arrange
        var registration = await AddRegistrationAsync();
        databaseMock
            .Setup(database => database.DeserializeConfig(SerializedConfig))
            .Returns(configMock.Object);
        configMock
            .Setup(config => config.ToDictionary())
            .Returns(new Dictionary<string, string> { ["ApiKey"] = "secret", ["Lang"] = "de" });
        databaseMock
            .Setup(database =>
                database.SerializeConfig(
                    It.Is<IReadOnlyDictionary<string, string>>(config =>
                        config["ApiKey"] == "updated" && config["Lang"] == "de"
                    )
                )
            )
            .Returns("{\"ApiKey\":\"updated\",\"Lang\":\"de\"}");

        // Act
        await service.UpdateAsync(
            registration.Id,
            new Dictionary<string, string> { ["ApiKey"] = "updated" },
            CancellationToken.None
        );

        // Assert
        var updated = await DbContext.MediaDatabaseRegistrations.SingleAsync();
        updated.SerializedConfig.ShouldBe("{\"ApiKey\":\"updated\",\"Lang\":\"de\"}");
    }

    [Test]
    public async Task TryLoginAsync_RegistrationExists_DelegatesToMediaDatabase()
    {
        // Arrange
        var registration = await AddRegistrationAsync();
        var loginResult = new TryLoginResult(IsSuccess: true, ErrorMessage: null);
        databaseMock
            .Setup(database => database.DeserializeConfig(SerializedConfig))
            .Returns(configMock.Object);
        databaseMock
            .Setup(database => database.TryLoginAsync(configMock.Object, CancellationToken.None))
            .ReturnsAsync(loginResult);

        // Act
        var result = await service.TryLoginAsync(registration.Id, CancellationToken.None);

        // Assert
        result.ShouldBe(loginResult);
        databaseMock.Verify(
            database => database.TryLoginAsync(configMock.Object, CancellationToken.None),
            Times.Once
        );
    }

    [Test]
    public async Task UpdateAsync_RegistrationHasUnreadableSecrets_ReplacesConfigAndClearsFlag()
    {
        // Arrange
        var registration = await AddRegistrationAsync(hasUnreadableSecrets: true);
        databaseMock
            .Setup(database =>
                database.SerializeConfig(
                    It.Is<IReadOnlyDictionary<string, string>>(config =>
                        config.Count == 1 && config["ApiKey"] == "updated"
                    )
                )
            )
            .Returns("{\"ApiKey\":\"updated\"}");

        // Act
        await service.UpdateAsync(
            registration.Id,
            new Dictionary<string, string> { ["ApiKey"] = "updated" },
            CancellationToken.None
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var updated = await DbContext.MediaDatabaseRegistrations.SingleAsync();
        updated.SerializedConfig.ShouldBe("{\"ApiKey\":\"updated\"}");
        updated.HasUnreadableSecrets.ShouldBeFalse();
        databaseMock.Verify(
            database => database.DeserializeConfig(It.IsAny<string>()),
            Times.Never
        );
    }

    [Test]
    public async Task ToggleIsActiveAsync_RegistrationExists_TogglesIsActive()
    {
        // Arrange
        var registration = await AddRegistrationAsync(isActive: true);

        // Act
        await service.ToggleIsActiveAsync(registration.Id, CancellationToken.None);

        // Assert
        var updated = await DbContext.MediaDatabaseRegistrations.SingleAsync();
        updated.IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task DeleteAsync_RegistrationExists_RemovesRegistration()
    {
        // Arrange
        var registration = await AddRegistrationAsync();

        // Act
        await service.DeleteAsync(registration.Id, CancellationToken.None);

        // Assert
        (await DbContext.MediaDatabaseRegistrations.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task GetAllAsync_SeveralRegistrations_ReturnsReadModelsOrderedByClassName()
    {
        // Arrange
        var registration = await AddRegistrationAsync();
        var otherRegistration = await AddRegistrationAsync(
            isActive: false,
            hasUnreadableSecrets: true,
            className: OtherClassName
        );
        var repository = CreateRepositoryWithDatabaseLookup();

        // Act
        var result = await repository.GetAllAsync(CancellationToken.None);

        // Assert
        result.ShouldBe([
            new MediaDatabaseRegistrationReadModel(
                otherRegistration.Id,
                IsActive: false,
                HasUnreadableSecrets: true,
                "Other database",
                OtherClassName
            ),
            new MediaDatabaseRegistrationReadModel(
                registration.Id,
                IsActive: true,
                HasUnreadableSecrets: false,
                "Test database",
                ClassName
            ),
        ]);
    }

    [Test]
    public async Task GetByIdAsync_RegistrationExists_ReturnsReadModel()
    {
        // Arrange
        await AddRegistrationAsync();
        var registration = await AddRegistrationAsync(
            hasUnreadableSecrets: true,
            className: OtherClassName
        );
        var repository = CreateRepositoryWithDatabaseLookup();

        // Act
        var result = await repository.GetByIdAsync(registration.Id, CancellationToken.None);

        // Assert
        result.ShouldBe(
            new MediaDatabaseRegistrationReadModel(
                registration.Id,
                IsActive: true,
                HasUnreadableSecrets: true,
                "Other database",
                OtherClassName
            )
        );
    }

    [Test]
    public async Task GetByIdAsync_RegistrationDoesNotExist_ReturnsNull()
    {
        // Arrange
        var registration = await AddRegistrationAsync();
        var repository = CreateRepositoryWithDatabaseLookup();

        // Act
        var result = await repository.GetByIdAsync(registration.Id + 1, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task ExistsForClassNameAsync_RegisteredAndUnregisteredClassName_ReturnsWhetherRegistered()
    {
        // Arrange
        await AddRegistrationAsync();
        var repository = CreateRepositoryWithDatabaseLookup();

        // Act
        var registeredExists = await repository.ExistsForClassNameAsync(
            ClassName,
            CancellationToken.None
        );
        var unregisteredExists = await repository.ExistsForClassNameAsync(
            OtherClassName,
            CancellationToken.None
        );

        // Assert
        registeredExists.ShouldBeTrue();
        unregisteredExists.ShouldBeFalse();
    }

    private MediaDatabaseRegistrationRepository CreateRepositoryWithDatabaseLookup()
    {
        databaseMock.Setup(database => database.Name).Returns("Test database");
        var otherDatabaseMock = new Mock<IMediaMetadataDatabase>(MockBehavior.Strict);
        otherDatabaseMock.Setup(database => database.Name).Returns("Other database");
        factoryMock
            .Setup(factory => factory.GetByClassName())
            .Returns(
                new Dictionary<string, IMediaMetadataDatabase>
                {
                    [ClassName] = databaseMock.Object,
                    [OtherClassName] = otherDatabaseMock.Object,
                }
            );

        return new MediaDatabaseRegistrationRepository(DbContext, DbContext, factoryMock.Object);
    }

    private async Task<MediaDatabaseRegistration> AddRegistrationAsync(
        bool isActive = true,
        bool hasUnreadableSecrets = false,
        string className = ClassName
    )
    {
        var registration = new MediaDatabaseRegistration
        {
            MediaDatabaseClassName = className,
            SerializedConfig = SerializedConfig,
            IsActive = isActive,
            HasUnreadableSecrets = hasUnreadableSecrets,
        };

        DbContext.MediaDatabaseRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return registration;
    }
}
