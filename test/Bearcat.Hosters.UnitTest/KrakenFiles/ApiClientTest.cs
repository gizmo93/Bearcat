using System.Net;
using Bearcat.Hosters.KrakenFiles;
using Bearcat.Hosters.KrakenFiles.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.KrakenFiles;

public class ApiClientTest
{
    private Mock<IKrakenFilesApi> apiMock = null!;
    private ApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IKrakenFilesApi>(MockBehavior.Strict);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<ILogger<ApiClient>>();

        apiClient = new ApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        );
    }

    [Test]
    public async Task IsApiKeyValidAsync_FileNotFoundForProbeHash_ReturnsTrue()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.GetFileAsync(
                    "bearcat-login-check",
                    "api-key",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateApiResponse<FileResponse>(HttpStatusCode.NotFound));

        // Act
        var result = await apiClient.IsApiKeyValidAsync(config, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task IsApiKeyValidAsync_UnauthorizedForProbeHash_ReturnsFalse()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.GetFileAsync(
                    "bearcat-login-check",
                    "api-key",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateApiResponse<FileResponse>(HttpStatusCode.Unauthorized));

        // Act
        var result = await apiClient.IsApiKeyValidAsync(config, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    private static ApiResponse<T> CreateApiResponse<T>(HttpStatusCode statusCode, T? content = default)
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(statusCode),
            content!,
            new RefitSettings(),
            error: null
        );
    }
}
