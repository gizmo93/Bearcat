using Bearcat.Abstractions.ImageHoster;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageImageHosters;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageImageHosters;

public class ImageHosterServiceTest : BearcatIntegrationTest
{
    private const string ImageHosterClassName = "TestImageHoster";
    private const string SerializedConfig = "{\"apiKey\":\"secret\"}";

    private BearcatDbContext dbContext = null!;
    private Mock<IImageHoster> imageHosterMock = null!;
    private Mock<IImageHosterConfig> imageHosterConfigMock = null!;
    private Mock<IImageHosterFactory> imageHosterFactoryMock = null!;
    private ImageHosterService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        imageHosterConfigMock = new Mock<IImageHosterConfig>(MockBehavior.Strict);
        imageHosterMock = new Mock<IImageHoster>(MockBehavior.Strict);
        imageHosterFactoryMock = new Mock<IImageHosterFactory>(MockBehavior.Strict);

        imageHosterFactoryMock
            .Setup(factory => factory.Get(ImageHosterClassName))
            .Returns(imageHosterMock.Object);

        service = new ImageHosterService(
            new ImageHosterRegistrationWriteRepository(dbContext),
            imageHosterFactoryMock.Object,
            NoOpSecretProtector.Instance
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
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
            CancellationToken.None
        );

        // Assert
        var registration = await dbContext.ImageHosterRegistrations.SingleAsync();

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
            CancellationToken.None
        );

        // Assert
        var result = await dbContext.ImageHosterRegistrations.SingleAsync();

        result.Name.ShouldBe("Updated image hoster");
        result.SerializedConfig.ShouldBe("{\"apiKey\":\"updated\"}");
        result.ImageHosterClassName.ShouldBe(ImageHosterClassName);
    }

    [Test]
    public async Task ToggleIsActiveAsync_RegistrationExists_TogglesIsActive()
    {
        // Arrange
        var registration = await AddImageHosterRegistrationAsync(isActive: true);

        // Act
        await service.ToggleIsActiveAsync(registration.Id, CancellationToken.None);

        // Assert
        var result = await dbContext.ImageHosterRegistrations.SingleAsync();

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
            .Setup(hoster =>
                hoster.TryLoginAsync(imageHosterConfigMock.Object, CancellationToken.None)
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
        var result = await dbContext.ImageHosterRegistrations.AnyAsync();

        result.ShouldBeFalse();
    }

    private async Task<ImageHosterRegistration> AddImageHosterRegistrationAsync(bool isActive)
    {
        var registration = new ImageHosterRegistration
        {
            Name = "Primary image hoster",
            IsActive = isActive,
            ImageHosterClassName = ImageHosterClassName,
            SerializedConfig = SerializedConfig,
        };

        dbContext.ImageHosterRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();

        return registration;
    }
}
