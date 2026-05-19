using System.Text.Json;
using Bearcat.LinkCrypters.KeepLinks;
using Bearcat.LinkCrypters.KeepLinks.Api;
using Moq;
using Shouldly;
using ProtectLinksResponse = Bearcat.LinkCrypters.KeepLinks.Api.ProtectLinks.Response;

namespace Bearcat.LinkCrypters.UnitTest.KeepLinks;

public class KeepLinksTest
{
    private Mock<IKeepLinksApi> apiMock = null!;
    private LinkCrypters.KeepLinks.KeepLinks service = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IKeepLinksApi>(MockBehavior.Strict);
        service = new LinkCrypters.KeepLinks.KeepLinks(apiMock.Object);
    }

    [Test]
    public async Task CreateContainerAsync_ApiProtectsLinks_ReturnsContainerLink()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };
        var links = new[] { "https://hoster.test/file-1", "https://hoster.test/file-2" };

        apiMock
            .Setup(x =>
                x.ProtectLinkAsync(
                    "api-key",
                    "https://hoster.test/file-1,https://hoster.test/file-2",
                    "password",
                    "container-name",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ProtectLinksResponse { ContainerLink = "https://keeplinks.org/p/abc" }
            );

        // Act
        var result = await service.CreateContainerAsync(
            config,
            "container-name",
            "password",
            links,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ContainerLink.ShouldBe("https://keeplinks.org/p/abc");
        result.ExternalReference.ShouldBeNull();
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task CreateContainerAsync_ApiReturnsError_ReturnsFailure()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.ProtectLinkAsync(
                    "api-key",
                    "https://hoster.test/file",
                    null,
                    "container-name",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProtectLinksResponse { ApiError = "invalid api key" });

        // Act
        var result = await service.CreateContainerAsync(
            config,
            "container-name",
            null,
            ["https://hoster.test/file"],
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ContainerLink.ShouldBeNull();
        result.ExternalReference.ShouldBeNull();
        result.ErrorMessages.ShouldBe(["invalid api key"]);
    }

    [Test]
    public async Task UpdateContainerAsync_ApiUpdatesContainer_ReturnsSuccess()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };
        var links = new[] { "https://hoster.test/file-1", "https://hoster.test/file-2" };

        apiMock
            .Setup(x =>
                x.UpdateContainerAsync(
                    "api-key",
                    "https://hoster.test/file-1,https://hoster.test/file-2",
                    "container-id",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProtectLinksResponse());

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://keeplinks.org/p/container-id",
            null,
            links,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task UpdateContainerAsync_ApiReturnsError_ReturnsFailure()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.UpdateContainerAsync(
                    "api-key",
                    "https://hoster.test/file",
                    "container-id",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProtectLinksResponse { ApiError = "update failed" });

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://keeplinks.org/p/container-id",
            null,
            ["https://hoster.test/file"],
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("update failed");
    }

    [Test]
    public async Task TryLoginAsync_ApiHashIsValid_ReturnsSuccess()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x => x.GetLinksAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"url_id":"container-id"}""");

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task TryLoginAsync_ApiHashIsInvalid_ReturnsFailure()
    {
        // Arrange
        var config = new KeepLinksConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x => x.GetLinksAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync("API hash is not valid");

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("API hash is not valid");
    }

    [Test]
    public void DeserializeConfig_SerializedConfig_ReturnsKeepLinksConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(new KeepLinksConfig { ApiKey = "api-key" });

        // Act
        var result = service.DeserializeConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<KeepLinksConfig>().ApiKey.ShouldBe("api-key");
    }
}
