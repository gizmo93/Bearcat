using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNfoDatabases;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageNfoDatabases;

public class NfoDatabaseRegistrationServiceTest : BearcatIntegrationTest
{
    private const string ClassName = "srrDB";
    private const string SerializedConfig = "{\"ApiKey\":\"secret\"}";

    private BearcatDbContext dbContext = null!;
    private Mock<INfoDatabase> databaseMock = null!;
    private Mock<INfoDatabaseConfig> configMock = null!;
    private Mock<INfoDatabaseFactory> factoryMock = null!;
    private NfoDatabaseRegistrationService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        databaseMock = new Mock<INfoDatabase>(MockBehavior.Strict);
        configMock = new Mock<INfoDatabaseConfig>(MockBehavior.Strict);
        factoryMock = new Mock<INfoDatabaseFactory>(MockBehavior.Strict);
        factoryMock.Setup(factory => factory.Get(ClassName)).Returns(databaseMock.Object);

        service = new NfoDatabaseRegistrationService(
            new NfoDatabaseRegistrationRepository(dbContext, dbContext, factoryMock.Object),
            factoryMock.Object,
            NoOpSecretProtector.Instance
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
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
        var registration = await dbContext.NfoDatabaseRegistrations.SingleAsync();
        registration.NfoDatabaseClassName.ShouldBe(ClassName);
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
        var updated = await dbContext.NfoDatabaseRegistrations.SingleAsync();
        updated.SerializedConfig.ShouldBe("{\"ApiKey\":\"updated\",\"Lang\":\"de\"}");
    }

    [Test]
    public async Task ToggleIsActiveAsync_RegistrationExists_TogglesIsActive()
    {
        // Arrange
        var registration = await AddRegistrationAsync(isActive: true);

        // Act
        await service.ToggleIsActiveAsync(registration.Id, CancellationToken.None);

        // Assert
        var updated = await dbContext.NfoDatabaseRegistrations.SingleAsync();
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
        (await dbContext.NfoDatabaseRegistrations.AnyAsync()).ShouldBeFalse();
    }

    private async Task<NfoDatabaseRegistration> AddRegistrationAsync(bool isActive = true)
    {
        var registration = new NfoDatabaseRegistration
        {
            NfoDatabaseClassName = ClassName,
            SerializedConfig = SerializedConfig,
            IsActive = isActive,
        };

        dbContext.NfoDatabaseRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return registration;
    }
}
