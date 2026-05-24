using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageHosters;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageHosters;

public class HosterRegistrationServiceTest : BearcatIntegrationTest
{
    private const string HosterClassName = "TestHoster";
    private const string SerializedConfig = "{\"apiKey\":\"secret\"}";

    private BearcatDbContext dbContext = null!;
    private Mock<IHoster> hosterMock = null!;
    private Mock<IHosterConfig> hosterConfigMock = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;
    private HosterRegistrationService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        hosterConfigMock = new Mock<IHosterConfig>(MockBehavior.Strict);
        hosterMock = new Mock<IHoster>(MockBehavior.Strict);
        hosterFactoryMock = new Mock<IHosterFactory>(MockBehavior.Strict);

        hosterFactoryMock.Setup(f => f.GetByName(HosterClassName)).Returns(hosterMock.Object);

        service = new HosterRegistrationService(
            new HosterConfigurationRepository(
                dbContext,
                dbContext,
                hosterFactoryMock.Object,
                NoOpSecretProtector.Instance
            ),
            hosterFactoryMock.Object
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task RegisterHosterAsync_ValidHoster_PersistsRegistrationAndReturnsId()
    {
        // Arrange
        var configuration = new Dictionary<string, string> { ["apiKey"] = "secret" };
        hosterMock.Setup(h => h.SerializeHosterConfig(configuration)).Returns(SerializedConfig);

        // Act
        var result = await service.RegisterHosterAsync(
            "Primary hoster",
            true,
            configuration,
            HosterClassName,
            CancellationToken.None
        );

        // Assert
        var registration = await dbContext.HosterRegistrations.SingleAsync();

        result.ShouldBeGreaterThan(0);
        registration.ShouldNotBeNull();
        registration.Id.ShouldBe(result);
        registration.Name.ShouldBe("Primary hoster");
        registration.IsActive.ShouldBeTrue();
        registration.HosterClassName.ShouldBe(HosterClassName);
        registration.SerializedConfig.ShouldBe(SerializedConfig);
        hosterFactoryMock.Verify(f => f.GetByName(HosterClassName), Times.Once);
        hosterMock.Verify(h => h.SerializeHosterConfig(configuration), Times.Once);
    }

    [Test]
    public async Task ToggleIsActiveAsync_RegistrationExists_TogglesIsActive()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(isActive: true);

        // Act
        await service.ToggleIsActiveAsync(registration.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.HosterRegistrations.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(registration.Id);
        result.IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task UpdateRegistrationAsync_RegistrationExists_UpdatesNameAndSerializedConfig()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(isActive: true);
        var configuration = new Dictionary<string, string> { ["apiKey"] = "updated" };
        hosterMock
            .Setup(h => h.SerializeHosterConfig(configuration))
            .Returns("{\"apiKey\":\"updated\"}");

        // Act
        await service.UpdateRegistrationAsync(
            registration.Id,
            "Updated hoster",
            configuration,
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.HosterRegistrations.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(registration.Id);
        result.Name.ShouldBe("Updated hoster");
        result.SerializedConfig.ShouldBe("{\"apiKey\":\"updated\"}");
        result.HosterClassName.ShouldBe(HosterClassName);
        hosterFactoryMock.Verify(f => f.GetByName(HosterClassName), Times.Once);
        hosterMock.Verify(h => h.SerializeHosterConfig(configuration), Times.Once);
    }

    [Test]
    public async Task TryLoginAsync_RegistrationExists_DelegatesToHoster()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(isActive: true);
        var loginResult = new TryLoginResult(true, null);
        hosterMock
            .Setup(h => h.DeserializeHosterConfig(SerializedConfig))
            .Returns(hosterConfigMock.Object);
        hosterMock
            .Setup(h => h.TryLoginAsync(hosterConfigMock.Object, CancellationToken.None))
            .ReturnsAsync(loginResult);

        // Act
        var result = await service.TryLoginAsync(registration.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(loginResult);
        hosterFactoryMock.Verify(f => f.GetByName(HosterClassName), Times.Once);
        hosterMock.Verify(h => h.DeserializeHosterConfig(SerializedConfig), Times.Once);
        hosterMock.Verify(
            h => h.TryLoginAsync(hosterConfigMock.Object, CancellationToken.None),
            Times.Once
        );
    }

    [Test]
    public async Task RemoveAsync_RegistrationExists_RemovesRegistration()
    {
        // Arrange
        var registration = await AddHosterRegistrationAsync(isActive: true);

        // Act
        await service.RemoveAsync(registration.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.HosterRegistrations.AnyAsync();

        result.ShouldBeFalse();
    }

    private async Task<HosterRegistration> AddHosterRegistrationAsync(bool isActive)
    {
        var registration = new HosterRegistration
        {
            Name = "Primary hoster",
            IsActive = isActive,
            HosterClassName = HosterClassName,
            SerializedConfig = SerializedConfig,
        };

        dbContext.HosterRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();

        return registration;
    }
}
