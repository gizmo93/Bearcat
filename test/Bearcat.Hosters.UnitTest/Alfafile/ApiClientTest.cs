using System.Net;
using Bearcat.Hosters.Alfafile;
using Bearcat.Hosters.Alfafile.Api;
using Bearcat.Hosters.Alfafile.Api.File;
using Bearcat.Hosters.Alfafile.Api.User;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Alfafile;

public class ApiClientTest
{
    private readonly AlfafileConfig config = new()
    {
        Username = "user@example.test",
        Password = "password",
    };

    private Mock<IAlfafileApi> apiMock = null!;
    private ApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IAlfafileApi>(MockBehavior.Strict);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<ILogger<ApiClient>>();

        apiMock
            .Setup(x => x.LoginAsync(config.Username, config.Password, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    new LoginResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Response = new LoginResponse.ResponseObject { Token = "auth-token" },
                    }
                )
            );

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
            .Select(index => $"https://alfafile.net/file/file-{index}")
            .ToList();
        var currentParallelRequests = 0;
        var maximumParallelRequests = 0;

        apiMock
            .Setup(x => x.GetFileInfoAsync("auth-token", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                var current = Interlocked.Increment(ref currentParallelRequests);
                UpdateMaximum(ref maximumParallelRequests, current);

                await Task.Delay(25);

                Interlocked.Decrement(ref currentParallelRequests);

                return CreateApiResponse(
                    new FileInfoResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Response = new FileInfoResponse.ResponseObject { File = new UploadedFile() },
                    }
                );
            });

        // Act
        var result = await apiClient.CheckLinksAsync(config, fileUrls, CancellationToken.None);

        // Assert
        result.Count.ShouldBe(25);
        result.Values.ShouldAllBe(isOnline => isOnline);
        maximumParallelRequests.ShouldBe(10);
    }

    [Test]
    public async Task CheckLinksAsync_TooManyRequests_RetriesLink()
    {
        // Arrange
        var fileUrl = "https://alfafile.net/file/file-1";
        var calls = 0;

        apiMock
            .Setup(x => x.GetFileInfoAsync("auth-token", "file-1", It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls++;

                if (calls == 1)
                {
                    return Task.FromResult(
                        CreateApiResponse(
                            new FileInfoResponse(),
                            HttpStatusCode.TooManyRequests
                        )
                    );
                }

                return Task.FromResult(
                    CreateApiResponse(
                        new FileInfoResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                            Response = new FileInfoResponse.ResponseObject
                            {
                                File = new UploadedFile(),
                            },
                        }
                    )
                );
            });

        // Act
        var result = await apiClient.CheckLinksAsync(config, [fileUrl], CancellationToken.None);

        // Assert
        result[fileUrl].ShouldBeTrue();
        calls.ShouldBe(2);
    }

    private static ApiResponse<T> CreateApiResponse<T>(
        T content,
        HttpStatusCode statusCode = HttpStatusCode.OK
    )
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(statusCode),
            content,
            new RefitSettings(),
            error: null
        );
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
