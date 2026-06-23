using System.Net;
using System.Net.Http.Headers;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.UploadG;
using Bearcat.Hosters.UploadG.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.UploadG;

public class ApiClientTest
{
    private Mock<IUploadGApi> apiMock = null!;
    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private ApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IUploadGApi>(MockBehavior.Strict);
        httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<ApiClient>>();

        apiClient = new ApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        );
    }

    [Test]
    public async Task UploadFileAsync_AlwaysUsesMultipartFlowAndPassesFolderIdToEntry()
    {
        // Arrange
        var config = new UploadGConfig { ApiKey = "api-key" };
        var httpMessageHandler = new TestHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers = { ETag = new EntityTagHeaderValue("\"etag-1\"") },
            }
        );
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(new HttpClient(httpMessageHandler));

        apiMock
            .Setup(x =>
                x.CreateMultipartUploadAsync(
                    "Bearer api-key",
                    It.Is<MultipartCreateRequest>(request =>
                        request.Filename == "file"
                        && request.Extension == "bin"
                        && request.Size == 3
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new MultipartCreateResponse(
                    Status: "success",
                    Key: "uploads/uuid/storage-name",
                    UploadId: "upload-id",
                    StorageBucket: "bucket",
                    Acl: "private"
                )
            );

        apiMock
            .Setup(x =>
                x.SignPartUrlsAsync(
                    "Bearer api-key",
                    It.Is<BatchSignPartUrlsRequest>(request =>
                        request.PartNumbers.SequenceEqual(new[] { 1 })
                        && request.UploadId == "upload-id"
                        && request.Key == "uploads/uuid/storage-name"
                        && request.StorageBucket == "bucket"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new BatchSignPartUrlsResponse(
                    Status: "success",
                    Urls: [new SignedPartUrl("https://uploads.uploadg.test/part-1", 1)]
                )
            );

        apiMock
            .Setup(x =>
                x.CompleteMultipartUploadAsync(
                    "Bearer api-key",
                    It.Is<MultipartCompleteRequest>(request =>
                        request.Parts.Count == 1
                        && request.Parts[0].ETag == "\"etag-1\""
                        && request.Parts[0].PartNumber == 1
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new StatusResponse("success"));

        apiMock
            .Setup(x =>
                x.CreateS3EntryAsync(
                    "Bearer api-key",
                    It.Is<CreateS3EntryRequest>(request =>
                        request.ClientName == "file.bin"
                        && request.Filename == "storage-name"
                        && request.ParentId == 42
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFileResponse(
                    Status: "success",
                    FileEntry: new FileEntry(17, "file.bin", 42, "text")
                )
            );

        // Act
        await using var stream = new MemoryStream([1, 2, 3]);
        var result = await apiClient.UploadFileAsync(
            config,
            stream,
            "file.bin",
            "42",
            3,
            CancellationToken.None
        );

        // Assert
        result.FileEntry?.Id.ShouldBe(17);
        httpMessageHandler.Request.ShouldNotBeNull();
        httpMessageHandler.Request!.Method.ShouldBe(HttpMethod.Put);
        httpMessageHandler
            .Request.RequestUri?.ToString()
            .ShouldBe("https://uploads.uploadg.test/part-1");
        httpMessageHandler.Request.Content?.Headers.ContentType.ShouldBeNull();
        httpMessageHandler.Request.Content?.Headers.ContentLength.ShouldBe(3);
        httpMessageHandler.Body.ShouldBe([1, 2, 3]);
    }

    [Test]
    public async Task UploadFileAsync_SignedUrlRequestTimesOut_ThrowsTimeoutExceptionBeforePartUpload()
    {
        // Arrange
        apiClient.FastApiRequestTimeout = TimeSpan.FromMilliseconds(20);
        var config = new UploadGConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.CreateMultipartUploadAsync(
                    "Bearer api-key",
                    It.IsAny<MultipartCreateRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new MultipartCreateResponse(
                    Status: "success",
                    Key: "uploads/uuid/storage-name",
                    UploadId: "upload-id",
                    StorageBucket: "bucket",
                    Acl: "private"
                )
            );

        apiMock
            .Setup(x =>
                x.SignPartUrlsAsync(
                    "Bearer api-key",
                    It.IsAny<BatchSignPartUrlsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (
                    string authorization,
                    BatchSignPartUrlsRequest request,
                    CancellationToken cancellationToken
                ) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

                    return new BatchSignPartUrlsResponse(Status: "success", Urls: []);
                }
            );

        await using var stream = new MemoryStream([1, 2, 3]);

        // Act
        var ex = await Should.ThrowAsync<TimeoutException>(() =>
            apiClient.UploadFileAsync(config, stream, "file.bin", "42", 3, CancellationToken.None)
        );

        // Assert
        ex.Message.ShouldContain("UploadG signed URL request for part 1 timed out after");
        httpClientFactoryMock.Verify(
            x => x.CreateClient(HttpClientProvider.UploadHttpClientName),
            Times.Never
        );
    }

    [Test]
    public async Task CreateFolderAsync_ExistingRootFolderWithName_ReturnsExistingFolderId()
    {
        // Arrange
        var config = new UploadGConfig { ApiKey = "api-key" };
        apiMock
            .Setup(x =>
                x.ListFileEntriesAsync(
                    "Bearer api-key",
                    50,
                    "folder",
                    "release-folder",
                    "0",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FileEntryListResponse(
                    Data:
                    [
                        new FileEntry(12, "release-folder", 99, "folder"),
                        new FileEntry(13, "release-folder", 0, "folder"),
                    ]
                )
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            config,
            "release-folder",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("13");
        apiMock.Verify(
            x =>
                x.CreateFolderAsync(
                    It.IsAny<string>(),
                    It.IsAny<CreateFolderRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task GetOrCreateShareableLinkAsync_ExistingLink_ReturnsPublicDriveUrl()
    {
        // Arrange
        var config = new UploadGConfig { ApiKey = "api-key" };
        apiMock
            .Setup(x =>
                x.GetShareableLinkAsync("Bearer api-key", 17, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new ShareableLinkResponse(
                        Status: "success",
                        Link: new ShareableLink(1, "hash-value", 17)
                    )
                )
            );

        // Act
        var result = await apiClient.GetOrCreateShareableLinkAsync(
            config,
            17,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("https://uploadg.com/drive/s/hash-value");
        apiMock.Verify(
            x =>
                x.CreateShareableLinkAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<CreateShareableLinkRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task CheckLinksAsync_EntryCheckThrows_OmitsLinkInsteadOfMarkingOffline()
    {
        // Arrange
        var config = new UploadGConfig { ApiKey = "api-key" };
        var fileUrl = "https://uploadg.com/drive/s/hash-value";

        apiMock
            .Setup(x =>
                x.GetShareableLinkAsync("Bearer api-key", 17, It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new HttpRequestException("network down"));

        // Act
        var result = await apiClient.CheckLinksAsync(
            config,
            [new FileUrlToCheckDto(fileUrl, "17")],
            CancellationToken.None
        );

        // Assert
        result.ShouldNotContainKey(fileUrl);
    }

    [Test]
    public async Task IsApiKeyValidAsync_SpaceUsageOk_ReturnsTrue()
    {
        // Arrange
        var config = new UploadGConfig { ApiKey = "api-key" };
        apiMock
            .Setup(x => x.GetSpaceUsageAsync("Bearer api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new SpaceUsageResponse(UsedSpace: 1, AvailableSpace: 2, PercentUsed: 50)
                )
            );

        // Act
        var result = await apiClient.IsApiKeyValidAsync(config, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
    }

    private static ApiResponse<T> CreateApiResponse<T>(HttpStatusCode statusCode, T content)
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(statusCode),
            content,
            new RefitSettings(),
            error: null
        );
    }

    private sealed class TestHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public byte[]? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Request = request;

            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            }

            return response;
        }
    }
}
