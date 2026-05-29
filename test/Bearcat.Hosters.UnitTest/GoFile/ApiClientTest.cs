using Bearcat.Hosters.GoFile.Api;
using Bearcat.Hosters.GoFile.Api.GetFileInfo;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using GoFileApiClient = Bearcat.Hosters.GoFile.Api.ApiClient;

namespace Bearcat.Hosters.UnitTest.GoFile;

public class ApiClientTest
{
    private Mock<IGoFileApi> apiMock = null!;
    private GoFileApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IGoFileApi>(MockBehavior.Strict);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<ILogger<GoFileApiClient>>();

        apiClient = new GoFileApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        )
        {
            RateLimitRetryDelay = TimeSpan.Zero,
            FileCheckTimeout = TimeSpan.FromSeconds(5),
        };
    }

    [Test]
    public async Task CheckOnlineStatusAsync_ManyLinks_ChecksUpToFiveLinksInParallel()
    {
        // Arrange
        var fileUrls = Enumerable
            .Range(1, 12)
            .Select(index => $"https://gofile.io/d/file-{index}")
            .ToList();
        var expectedParallelRequests = 5;
        var currentParallelRequests = 0;
        var maximumParallelRequests = 0;
        var firstBatchStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var allowRequestsToFinish = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var onlineResponse = new Response
        {
            Status = "ok",
            Data = new Data { Id = "file-id", Type = "file" },
        };

        apiMock
            .Setup(x =>
                x.GetFileInfoAsync(
                    It.IsAny<string>(),
                    "Bearer api-key",
                    "4fd6sg89d7s6",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
            {
                var current = Interlocked.Increment(ref currentParallelRequests);
                UpdateMaximum(ref maximumParallelRequests, current);

                if (current == expectedParallelRequests)
                {
                    firstBatchStarted.TrySetResult();
                }

                return allowRequestsToFinish.Task.ContinueWith(_ =>
                {
                    Interlocked.Decrement(ref currentParallelRequests);
                    return onlineResponse;
                });
            });

        // Act
        var checkTask = apiClient.CheckOnlineStatusAsync(
            fileUrls,
            "api-key",
            CancellationToken.None
        );

        await firstBatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        allowRequestsToFinish.TrySetResult();
        var result = await checkTask;

        // Assert
        result.Count.ShouldBe(12);
        result.Values.ShouldAllBe(status => status.IsOnline);
        result.Values.ShouldAllBe(status => status.ErrorMessage == null);
        maximumParallelRequests.ShouldBe(expectedParallelRequests);
    }

    [Test]
    public async Task CheckOnlineStatusAsync_FolderResponse_ReturnsOffline()
    {
        // Arrange
        var fileUrl = "https://gofile.io/d/folder-id";

        apiMock
            .Setup(x =>
                x.GetFileInfoAsync(
                    "folder-id",
                    "Bearer api-key",
                    "4fd6sg89d7s6",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Response
                {
                    Status = "ok",
                    Data = new Data { Id = "folder-id", Type = "folder" },
                }
            );

        // Act
        var result = await apiClient.CheckOnlineStatusAsync(
            [fileUrl],
            "api-key",
            CancellationToken.None
        );

        // Assert
        result[fileUrl].IsOnline.ShouldBeFalse();
        result[fileUrl].ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task CheckOnlineStatusAsync_EmptyUrl_ReturnsOfflineWithoutApiCall()
    {
        // Arrange
        var fileUrl = string.Empty;

        // Act
        var result = await apiClient.CheckOnlineStatusAsync(
            [fileUrl],
            "api-key",
            CancellationToken.None
        );

        // Assert
        result[fileUrl].IsOnline.ShouldBeFalse();
        result[fileUrl].ErrorMessage.ShouldBe("Invalid GoFile URL");
        apiMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task CheckOnlineStatusAsync_FileInfoRequestTimesOut_ReturnsError()
    {
        // Arrange
        var fileUrl = "https://gofile.io/d/file-id";
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<ILogger<GoFileApiClient>>();
        var timeoutApiClient = new GoFileApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        )
        {
            RateLimitRetryDelay = TimeSpan.Zero,
            FileCheckTimeout = TimeSpan.FromMilliseconds(10),
        };

        apiMock
            .Setup(x =>
                x.GetFileInfoAsync(
                    "file-id",
                    "Bearer api-key",
                    "4fd6sg89d7s6",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, string, string, CancellationToken>(
                async (_, _, _, cancellationToken) =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                    return new Response();
                }
            );

        // Act
        var result = await timeoutApiClient.CheckOnlineStatusAsync(
            [fileUrl],
            "api-key",
            CancellationToken.None
        );

        // Assert
        result[fileUrl].IsOnline.ShouldBeFalse();
        result[fileUrl].ErrorMessage.ShouldBe("GoFile file check timed out after 10 milliseconds");
    }

    private static void UpdateMaximum(ref int maximum, int current)
    {
        var initialMaximum = maximum;

        while (
            current > initialMaximum
            && Interlocked.CompareExchange(ref maximum, current, initialMaximum) != initialMaximum
        )
        {
            initialMaximum = maximum;
        }
    }
}
