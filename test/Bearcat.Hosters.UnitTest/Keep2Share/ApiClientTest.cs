using System.Net;
using Bearcat.Hosters.Keep2Share.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Keep2Share;

public class ApiClientTest
{
    private Mock<IKeep2ShareApi> apiMock = null!;
    private ApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IKeep2ShareApi>(MockBehavior.Strict);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<ILogger<ApiClient>>();

        apiClient = new ApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        )
        {
            RateLimitRetryDelay = TimeSpan.Zero,
        };
    }

    [Test]
    public async Task CheckLinksAsync_ManyLinks_ChecksUpToTenLinksInParallel()
    {
        // Arrange
        var fileUrls = Enumerable
            .Range(1, 25)
            .Select(index => $"https://k2s.cc/file/file-{index}")
            .ToList();
        var currentParallelRequests = 0;
        var maximumParallelRequests = 0;

        apiMock
            .Setup(x => x.GetFileStatusAsync(It.IsAny<FileStatusRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                var current = Interlocked.Increment(ref currentParallelRequests);
                UpdateMaximum(ref maximumParallelRequests, current);

                await Task.Delay(25);

                Interlocked.Decrement(ref currentParallelRequests);

                return new FileStatusResponse { Status = "success", IsAvailable = true };
            });

        // Act
        var result = await apiClient.CheckLinksAsync(fileUrls, CancellationToken.None);

        // Assert
        result.Count.ShouldBe(25);
        result.Values.ShouldAllBe(isOnline => isOnline);
        maximumParallelRequests.ShouldBe(10);
    }

    [Test]
    public async Task CheckLinksAsync_TooManyRequests_RetriesLink()
    {
        // Arrange
        var fileUrl = "https://k2s.cc/file/file-1";
        var calls = 0;

        apiMock
            .Setup(x => x.GetFileStatusAsync(It.IsAny<FileStatusRequest>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls++;

                if (calls == 1)
                {
                    throw new HttpRequestException(
                        "rate limited",
                        inner: null,
                        statusCode: HttpStatusCode.TooManyRequests
                    );
                }

                return Task.FromResult(
                    new FileStatusResponse { Status = "success", IsAvailable = true }
                );
            });

        // Act
        var result = await apiClient.CheckLinksAsync([fileUrl], CancellationToken.None);

        // Assert
        result[fileUrl].ShouldBeTrue();
        calls.ShouldBe(2);
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
