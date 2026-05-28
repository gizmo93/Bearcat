using System.Net;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.Keep2Share;
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
    public async Task CheckLinksAsync_ApiReturnsFileInfos_MapsStatusesToOriginalUrls()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };
        var fileUrls = new[]
        {
            "https://k2s.cc/file/online-id",
            "https://k2s.cc/file/offline-id",
            "https://k2s.cc/file/missing-id",
            "not-a-url",
        };

        apiMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new LoginResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    AuthToken = "auth-token",
                }
            );

        apiMock
            .Setup(x =>
                x.GetFilesInfoAsync(
                    It.Is<GetFilesInfoRequest>(request =>
                        request.AuthToken == "auth-token"
                        && request.Ids.Count == 3
                        && request.Ids.Contains("online-id")
                        && request.Ids.Contains("offline-id")
                        && request.Ids.Contains("missing-id")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new GetFilesInfoResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    Files =
                    [
                        new GetFilesInfoResponse.FileInfo
                        {
                            Id = "online-id",
                            IsAvailable = true,
                        },
                        new GetFilesInfoResponse.FileInfo
                        {
                            Id = "offline-id",
                            IsAvailable = false,
                        },
                        new GetFilesInfoResponse.FileInfo
                        {
                            Id = "missing-id",
                            IsAvailable = null,
                        },
                    ],
                }
            );

        // Act
        var result = await apiClient.CheckLinksAsync(config, fileUrls, CancellationToken.None);

        // Assert
        result[fileUrls[0]].ShouldBeTrue();
        result[fileUrls[1]].ShouldBeFalse();
        result[fileUrls[2]].ShouldBeFalse();
        result[fileUrls[3]].ShouldBeFalse();
        apiMock.Verify(
            x => x.GetFileStatusAsync(It.IsAny<FileStatusRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Test]
    public async Task CheckLinksAsync_TooManyRequests_RetriesBatch()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };
        var fileUrl = "https://k2s.cc/file/file-1";
        var calls = 0;

        apiMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new LoginResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    AuthToken = "auth-token",
                }
            );

        apiMock
            .Setup(x => x.GetFilesInfoAsync(It.IsAny<GetFilesInfoRequest>(), It.IsAny<CancellationToken>()))
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
                    new GetFilesInfoResponse
                    {
                        Status = "success",
                        Code = (int)HttpStatusCode.OK,
                        Files =
                        [
                            new GetFilesInfoResponse.FileInfo
                            {
                                Id = "file-1",
                                IsAvailable = true,
                            },
                        ],
                    }
                );
            });

        // Act
        var result = await apiClient.CheckLinksAsync(config, [fileUrl], CancellationToken.None);

        // Assert
        result[fileUrl].ShouldBeTrue();
        calls.ShouldBe(2);
    }

    [Test]
    public async Task RequestUploadAsync_UploadFormReturnsCaptchaError_ThrowsCaptchaRequired()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };

        apiMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new LoginResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    AuthToken = "auth-token",
                }
            );

        apiMock
            .Setup(x =>
                x.GetUploadFormDataAsync(
                    It.IsAny<UploadFormDataRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFormDataResponse
                {
                    Status = "error",
                    Code = (int)HttpStatusCode.BadRequest,
                    ErrorCode = 2,
                    Message = "Invalid request params",
                }
            );

        // Act + Assert
        var exception = await Should.ThrowAsync<CaptchaVerificationRequiredException>(
            () => apiClient.RequestUploadAsync(config, CancellationToken.None)
        );
        exception.Code.ShouldBe((int)HttpStatusCode.BadRequest);
        exception.ErrorCode.ShouldBe(2);
    }
}
