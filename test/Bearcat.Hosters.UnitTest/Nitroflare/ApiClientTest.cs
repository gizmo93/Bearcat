using System.Net;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.Nitroflare;
using Bearcat.Hosters.Nitroflare.Api;
using Bearcat.Hosters.Nitroflare.Api.File;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;
using HostedFileInfo = Bearcat.Abstractions.Hoster.Results.HostedFileInfo;

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

    [Test]
    public async Task TestUserHashAsync_UploadServerAnswersWrongHash_ThrowsHosterCredentialsRejected()
    {
        // Arrange
        var config = new NitroflareConfig { UserHash = "wrong-hash" };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(new HttpClient(new WrongHashUploadHandler()));
        var uploadingApiClient = new ApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            new Mock<ILogger<ApiClient>>().Object
        );
        apiMock
            .Setup(x => x.GetUploadServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://s12.nitroflare.test/");

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            uploadingApiClient.TestUserHashAsync(config, CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldBe("Invalid User - Wrong hash");
    }

    [Test]
    public async Task GetFileInfosAsync_ApiReturnsOnlineAndOfflineFiles_ReturnsNameAndSizeOfOnlineFiles()
    {
        // Arrange
        apiMock
            .Setup(x => x.GetFileInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateResponse(
                    HttpStatusCode.OK,
                    new FileInfoResponse
                    {
                        Type = "success",
                        Result = new FileInfoResult
                        {
                            Files = new Dictionary<string, NitroflareFile>
                            {
                                ["C6AAA81564F4BE7"] = new NitroflareFile
                                {
                                    Status = "online",
                                    Name = "archive.part01.rar",
                                    Size = 1049624579,
                                },
                                ["22B25DB30AA33C3"] = new NitroflareFile
                                {
                                    Status = "offline",
                                    Name = "archive.part48.rar",
                                    Size = 106954758,
                                },
                            },
                        },
                    }
                )
            );

        // Act
        var result = await apiClient.GetFileInfosAsync(
            ["C6AAA81564F4BE7", "22B25DB30AA33C3", "AAAAAAAAAAAAAAA"],
            CancellationToken.None
        );

        // Assert
        result["C6AAA81564F4BE7"]
            .ShouldBe(
                new HostedFileInfo(
                    IsOnline: true,
                    FileName: "archive.part01.rar",
                    SizeBytes: 1049624579
                )
            );
        result["22B25DB30AA33C3"]
            .ShouldBe(new HostedFileInfo(IsOnline: false, FileName: null, SizeBytes: null));
        result.ShouldNotContainKey("AAAAAAAAAAAAAAA");
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
            new HttpResponseMessage(statusCode) { RequestMessage = new HttpRequestMessage() },
            content!,
            new RefitSettings(),
            error: null
        );
    }

    private sealed class WrongHashUploadHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("Invalid User - Wrong hash"),
                }
            );
        }
    }
}
