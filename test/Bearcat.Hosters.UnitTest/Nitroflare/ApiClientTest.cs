using System.Net;
using Bearcat.Hosters.Nitroflare.Api;
using Bearcat.Hosters.Nitroflare.Api.File;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Nitroflare;

public class ApiClientTest
{
    private Mock<INitroflareApi> apiMock = null!;
    private ApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<INitroflareApi>(MockBehavior.Strict);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<ApiClient>>();

        apiClient = new ApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        );
    }

    [Test]
    public async Task CheckLinksAsync_ApiReturnsStatuses_MapsOnlineAndOfflinePerUrl()
    {
        // Arrange
        var onlineUrl = "https://nitroflare.com/view/online-id";
        var offlineUrl = "https://nitroflare.com/view/offline-id";

        apiMock
            .Setup(x => x.GetFileInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateSuccessResponse(
                    new Dictionary<string, string>
                    {
                        ["online-id"] = "online",
                        ["offline-id"] = "offline",
                    }
                )
            );

        // Act
        var result = await apiClient.CheckLinksAsync(
            [onlineUrl, offlineUrl],
            CancellationToken.None
        );

        // Assert
        result[onlineUrl].ShouldBeTrue();
        result[offlineUrl].ShouldBeFalse();
    }

    [Test]
    public async Task CheckLinksAsync_BatchRequestFails_OmitsAffectedUrlsInsteadOfMarkingOffline()
    {
        // Arrange
        var fileUrl = "https://nitroflare.com/view/unknown-id";

        apiMock
            .Setup(x => x.GetFileInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResponse(HttpStatusCode.ServiceUnavailable, content: null));

        // Act
        var result = await apiClient.CheckLinksAsync([fileUrl], CancellationToken.None);

        // Assert
        result.ShouldNotContainKey(fileUrl);
    }

    [Test]
    public async Task CheckLinksAsync_BatchThrows_OmitsAffectedUrlsAndDoesNotThrow()
    {
        // Arrange
        var fileUrl = "https://nitroflare.com/view/unknown-id";

        apiMock
            .Setup(x => x.GetFileInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));

        // Act
        var result = await apiClient.CheckLinksAsync([fileUrl], CancellationToken.None);

        // Assert
        result.ShouldNotContainKey(fileUrl);
    }

    [Test]
    public async Task CheckLinksAsync_OneBatchThrows_KeepsResultsOfSuccessfulBatch()
    {
        // Arrange
        var onlineUrls = Enumerable
            .Range(start: 1, count: 100)
            .Select(index => $"https://nitroflare.com/view/online-{index}")
            .ToList();
        var throwingUrl = "https://nitroflare.com/view/throws";

        apiMock
            .Setup(x =>
                x.GetFileInfoAsync(
                    It.Is<string>(files => files.Contains("online-")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (string files, CancellationToken _) =>
                    CreateSuccessResponse(files.Split(',').ToDictionary(id => id, _ => "online"))
            );

        apiMock
            .Setup(x =>
                x.GetFileInfoAsync(
                    It.Is<string>(files => files.Contains("throws")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("network down"));

        // Act
        var result = await apiClient.CheckLinksAsync(
            [.. onlineUrls, throwingUrl],
            CancellationToken.None
        );

        // Assert
        foreach (var onlineUrl in onlineUrls)
        {
            result[onlineUrl].ShouldBeTrue();
        }

        result.ShouldNotContainKey(throwingUrl);
    }

    private static ApiResponse<FileInfoResponse> CreateSuccessResponse(
        IReadOnlyDictionary<string, string> statusByFileId
    )
    {
        return CreateResponse(
            HttpStatusCode.OK,
            new FileInfoResponse
            {
                Type = "success",
                Result = new FileInfoResult
                {
                    Files = statusByFileId.ToDictionary(
                        item => item.Key,
                        item => new NitroflareFile { Status = item.Value }
                    ),
                },
            }
        );
    }

    private static ApiResponse<FileInfoResponse> CreateResponse(
        HttpStatusCode statusCode,
        FileInfoResponse? content
    )
    {
        return new ApiResponse<FileInfoResponse>(
            new HttpResponseMessage(statusCode),
            content!,
            new RefitSettings(),
            error: null
        );
    }
}
