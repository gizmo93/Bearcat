using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.LinkCrypter.Results;
using Bearcat.LinkCrypters.HideCx;
using Bearcat.LinkCrypters.HideCx.Api;
using Moq;
using Refit;
using Shouldly;
using CreateContainerRequest = Bearcat.LinkCrypters.HideCx.Api.CreateContainer.Request;
using CreateContainerResponse = Bearcat.LinkCrypters.HideCx.Api.CreateContainer.Response;
using SearchContainersRequest = Bearcat.LinkCrypters.HideCx.Api.SearchContainers.Request;
using SearchContainersResponse = Bearcat.LinkCrypters.HideCx.Api.SearchContainers.Response;
using UpdateContainerRequest = Bearcat.LinkCrypters.HideCx.Api.UpdateContainer.Request;

namespace Bearcat.LinkCrypters.UnitTest.HideCx;

public class HideCxTest
{
    private Mock<IHideCxApi> apiMock = null!;
    private LinkCrypters.HideCx.HideCx service = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IHideCxApi>(MockBehavior.Strict);
        service = new LinkCrypters.HideCx.HideCx(apiMock.Object);
    }

    [Test]
    public async Task CreateContainerAsync_ApiCreatesContainer_ReturnsContainerLinkAndExternalReference()
    {
        // Arrange
        var config = new HideCxConfig { ApiKey = "api-key" };
        var links = new[] { "https://hoster.test/file-1", "https://hoster.test/file-2" };

        apiMock
            .Setup(x =>
                x.CreateContainerAsync(
                    It.Is<CreateContainerRequest>(request =>
                        request.Name == "container-name"
                        && request.Password == "password"
                        && request.Mirrors.Length == 1
                        && request.Mirrors[0].SequenceEqual(links)
                    ),
                    "Bearer api-key",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new CreateContainerResponse
                {
                    Id = "external-id",
                    CanonicalUrl = "https://hide.cx/container",
                }
            );

        // Act
        var result = await service.CreateContainerAsync(
            config,
            "container-name",
            "password",
            links,
            enableCaptcha: true,
            enableContainerDownload: true,
            enableClickAndLoad: true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ContainerLink.ShouldBe("https://hide.cx/container");
        result.ExternalReference.ShouldBe("external-id");
        result.StatusImageId.ShouldBe("external-id");
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task CreateContainerAsync_ApiThrows_ReturnsFailure()
    {
        // Arrange
        var config = new HideCxConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.CreateContainerAsync(
                    It.IsAny<CreateContainerRequest>(),
                    "Bearer api-key",
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("create failed"));

        // Act
        var result = await service.CreateContainerAsync(
            config,
            "container-name",
            null,
            ["https://hoster.test/file"],
            enableCaptcha: true,
            enableContainerDownload: true,
            enableClickAndLoad: true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ContainerLink.ShouldBeNull();
        result.ExternalReference.ShouldBeNull();
        result.ErrorMessages.ShouldBe(["create failed"]);
    }

    [Test]
    public async Task UpdateContainerAsync_ApiUpdatesContainer_ReturnsSuccess()
    {
        // Arrange
        var config = new HideCxConfig { ApiKey = "api-key" };
        var links = new[] { "https://hoster.test/file-1", "https://hoster.test/file-2" };

        apiMock
            .Setup(x =>
                x.UpdateContainerAsync(
                    "external-id",
                    It.Is<UpdateContainerRequest>(request =>
                        request.Mirrors.Length == 1 && request.Mirrors[0].SequenceEqual(links)
                    ),
                    "Bearer api-key",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("ok");

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://hide.cx/container",
            "external-id",
            null,
            links,
            enableCaptcha: true,
            enableContainerDownload: true,
            enableClickAndLoad: true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.Outcome.ShouldBe(UpdateContainerOutcome.Success);
        result.ErrorMessage.ShouldBeNull();
        result.StatusImageId.ShouldBe("external-id");
    }

    [Test]
    public async Task UpdateContainerAsync_PatchFailsAndContainerExists_ReturnsFailed()
    {
        // Arrange
        var config = new HideCxConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.UpdateContainerAsync(
                    "external-id",
                    It.IsAny<UpdateContainerRequest>(),
                    "Bearer api-key",
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("update failed"));
        apiMock
            .Setup(x =>
                x.GetContainerAsync("external-id", "Bearer api-key", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync("""{"id":"external-id"}""");

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://hide.cx/container",
            "external-id",
            null,
            ["https://hoster.test/file"],
            enableCaptcha: true,
            enableContainerDownload: true,
            enableClickAndLoad: true,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.Outcome.ShouldBe(UpdateContainerOutcome.Failed);
        result.ErrorMessage.ShouldBe("update failed");
    }

    [Test]
    public async Task UpdateContainerAsync_PatchFailsAndContainerIsNotFound_ReturnsContainerNotFound()
    {
        // Arrange
        var config = new HideCxConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.UpdateContainerAsync(
                    "external-id",
                    It.IsAny<UpdateContainerRequest>(),
                    "Bearer api-key",
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                await CreateApiExceptionAsync(
                    HttpMethod.Patch,
                    HttpStatusCode.InternalServerError,
                    """{"error":"internal server error"}"""
                )
            );
        apiMock
            .Setup(x =>
                x.GetContainerAsync("external-id", "Bearer api-key", It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(
                await CreateApiExceptionAsync(
                    HttpMethod.Get,
                    HttpStatusCode.NotFound,
                    """{"error":"container not found or invalid"}"""
                )
            );

        // Act
        var result = await service.UpdateContainerAsync(
            config,
            "https://hide.cx/container",
            "external-id",
            null,
            ["https://hoster.test/file"],
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Outcome.ShouldBe(UpdateContainerOutcome.ContainerNotFound);
    }

    [Test]
    public async Task TryLoginAsync_SearchSucceeds_ReturnsSuccess()
    {
        // Arrange
        var config = new HideCxConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.SearchContainersAsync(
                    It.Is<SearchContainersRequest>(request =>
                        request.Limit == 1
                        && request.Offset == 0
                        && request.AccessStatus == "unknown"
                    ),
                    "Bearer api-key",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new SearchContainersResponse { Total = 0 });

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task TryLoginAsync_SearchThrows_ReturnsFailure()
    {
        // Arrange
        var config = new HideCxConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.SearchContainersAsync(
                    It.IsAny<SearchContainersRequest>(),
                    "Bearer api-key",
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("invalid token"));

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("invalid token");
    }

    [Test]
    public void DeserializeConfig_SerializedConfig_ReturnsHideCxConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(new HideCxConfig { ApiKey = "api-key" });

        // Act
        var result = service.DeserializeConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<HideCxConfig>().ApiKey.ShouldBe("api-key");
    }

    private static async Task<ApiException> CreateApiExceptionAsync(
        HttpMethod method,
        HttpStatusCode statusCode,
        string content
    )
    {
        return await ApiException.Create(
            new HttpRequestMessage(method, "https://hide.cx/containers/external-id"),
            method,
            new HttpResponseMessage(statusCode) { Content = new StringContent(content) },
            new RefitSettings()
        );
    }
}
