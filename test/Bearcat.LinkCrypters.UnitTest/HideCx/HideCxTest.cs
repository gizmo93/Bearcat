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

    [Test]
    public async Task ListContainersAsync_SearchReturnsContainers_ReturnsContainersExceptDeleted()
    {
        // Arrange
        var config = new HideCxConfig { ApiKey = "api-key" };
        var response = JsonSerializer.Deserialize<SearchContainersResponse>(
            """
            {"containers":[
              {"id":"6b4205c5-a07b-407a-9c93-aae361b1135f","name":"First.Release","access_status":"online","links":[],"canonical_url":"https://hide.cx/container/6b4205c5-a07b-407a-9c93-aae361b1135f"},
              {"id":"c679078e-485d-46f1-8758-e04b27dfc17c","name":"Deleted.Release","access_status":"deleted","links":[],"canonical_url":"https://hide.cx/container/c679078e-485d-46f1-8758-e04b27dfc17c"}
            ],"total":2}
            """
        )!;

        apiMock
            .Setup(x =>
                x.SearchContainersAsync(
                    It.Is<SearchContainersRequest>(request =>
                        request.Offset == 0
                        && request.Limit == 500
                        && request.AccessStatus == null
                        && request.OrderBy == "created_at"
                        && request.OrderType == "asc"
                    ),
                    "Bearer api-key",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(response);

        // Act
        var result = await service.ListContainersAsync(config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Containers.Count.ShouldBe(1);
        result.Containers[0].ContainerId.ShouldBe("6b4205c5-a07b-407a-9c93-aae361b1135f");
        result
            .Containers[0]
            .ContainerUrl.ShouldBe(
                "https://hide.cx/container/6b4205c5-a07b-407a-9c93-aae361b1135f"
            );
        result.Containers[0].ContainerName.ShouldBe("First.Release");
        result.Containers[0].ExternalReference.ShouldBe("6b4205c5-a07b-407a-9c93-aae361b1135f");
    }

    [Test]
    public async Task ReadContainerLinksAsync_ContainerHasLinksOfTwoMirrors_ReturnsHosterUrlsGroupedPerMirror()
    {
        // Arrange
        var config = new HideCxConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.GetContainerAsync("external-id", "Bearer api-key", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                """
                {"id":"external-id","name":"Release","links":[
                  {"id":"1","host":"rapidgator.net","link_type":"mirror_2","link_status":"online","link_size":1,"file":"a","name":"a.rar","hoster_url":"https://rapidgator.net/file/a"},
                  {"id":"2","host":"nitroflare.com","link_type":"mirror_1","link_status":"online","link_size":1,"file":"b","name":"b.part1.rar","hoster_url":"https://nitroflare.com/view/B1/b.part1.rar"},
                  {"id":"3","host":"nitroflare.com","link_type":"mirror_1","link_status":"unknown","link_size":null,"file":"c","name":"b.part2.rar","hoster_url":"https://nitroflare.com/view/B2/b.part2.rar"}
                ]}
                """
            );

        // Act
        var result = await service.ReadContainerLinksAsync(
            linkCrypterConfig: config,
            containerUrl: "https://hide.cx/container/external-id",
            externalReference: "external-id",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.LinksPerMirror.Count.ShouldBe(2);
        result
            .LinksPerMirror[0]
            .ShouldBe([
                "https://nitroflare.com/view/B1/b.part1.rar",
                "https://nitroflare.com/view/B2/b.part2.rar",
            ]);
        result.LinksPerMirror[1].ShouldBe(["https://rapidgator.net/file/a"]);
        result.HasBackupLinks.ShouldBeFalse();
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
