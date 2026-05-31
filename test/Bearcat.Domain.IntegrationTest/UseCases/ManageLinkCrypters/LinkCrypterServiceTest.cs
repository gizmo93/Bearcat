using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageLinkCrypters;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageLinkCrypters;

public class LinkCrypterServiceTest : BearcatIntegrationTest
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
            linkCrypterFactoryMock.Object,
            NoOpSecretProtector.Instance
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
            CancellationToken.None
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
            CancellationToken.None
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

    private async Task<LinkCrypterRegistration> AddLinkCrypterRegistrationAsync(bool isActive)
    {
        var registration = new LinkCrypterRegistration
        {
            Name = "Primary crypter",
            IsActive = isActive,
            LinkCrypterClassName = LinkCrypterClassName,
            SerializedConfig = SerializedConfig,
        };

        dbContext.LinkCrypterRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();

        return registration;
    }
}
