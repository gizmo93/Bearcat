using System.Text.Json;
using Bearcat.Hosters.FileQ.Api;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.FileQ;

public class ApiClientTest
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private Mock<IFileQApi> apiMock = null!;
    private ApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IFileQApi>(MockBehavior.Strict);
        var httpClientProvider = new HttpClientProvider(new Mock<IHttpClientFactory>().Object);

        apiClient = new ApiClient(
            apiMock.Object,
            httpClientProvider,
            new HosterFileDownloader(httpClientProvider)
        );
    }

    [TestCase("""{"msg":"OK","status":200}""")]
    [TestCase("""{"msg":"No folder","status":404}""")]
    public async Task DeleteFolderAsync_SuccessOrFolderNotFound_ReturnsWithoutThrowing(string json)
    {
        // Arrange
        apiMock
            .As<IXFilesharingApi>()
            .Setup(x => x.DeleteFolderAsync("api-key", "52871", It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Deserialize<StatusResponse>(json, JsonSerializerOptions)!);

        // Act
        await apiClient.DeleteFolderAsync("api-key", "52871", CancellationToken.None);

        // Assert
        apiMock
            .As<IXFilesharingApi>()
            .Verify(
                x => x.DeleteFolderAsync("api-key", "52871", It.IsAny<CancellationToken>()),
                Times.Once
            );
    }
}
