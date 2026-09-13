using System.Net;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.CloudFam;
using Bearcat.Hosters.CloudFam.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.CloudFam;

public class ApiClientTest
{
    private const string ApiKey = "cloudfam-api-key";

    private const string FileCode = "b578rni0e1ka";

    private const string UploadUrl = "https://cloudfam.r2.cloudflarestorage.com/temp-uploads/1";

    private const string UploadKey = "temp-uploads/2688/933c7cb9f990259318c367e449bf17ca";

    private Mock<ICloudFamApi> apiMock = null!;
    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private ApiClient apiClient = null!;
    private CloudFamConfig config = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        apiMock = new Mock<ICloudFamApi>(MockBehavior.Strict);
        httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<ApiClient>>();

        apiClient = new ApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        )
        {
            FinalizationRecoveryDelay = TimeSpan.Zero,
        };

        config = new CloudFamConfig { ApiKey = ApiKey };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        await File.WriteAllTextAsync(filePath, "cloudfam test content");
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_NewFile_StreamsBytesToStorageAndFinalizesUpload()
    {
        // Arrange
        var httpMessageHandler = SetupStorageHandler(HttpStatusCode.OK);
        SetupUploadSession();
        SetupFinalizeUpload();

        // Act
        var result = await UploadAsync(folderId: null);

        // Assert
        result.FileId.ShouldBe("139473");
        result.FileUrl.ShouldBe($"https://cloudfam.io/{FileCode}");
        httpMessageHandler.UploadedBytes.ShouldBe(new FileInfo(filePath).Length);
        httpMessageHandler.Method.ShouldBe(HttpMethod.Put);
        apiMock.VerifyAll();
    }

    [Test]
    public async Task UploadFileAsync_WithFolder_MovesFinalizedFileIntoFolder()
    {
        // Arrange
        SetupStorageHandler(HttpStatusCode.OK);
        SetupUploadSession();
        SetupFinalizeUpload();

        apiMock
            .Setup(x =>
                x.MoveFilesAsync(
                    ApiKey,
                    It.Is<MoveFilesRequest>(request =>
                        request.FolderId == 15 && request.FileIds.Single() == 139473
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateEnvelope(new MoveFilesResponse(15, MovedCount: 1)));

        // Act
        await UploadAsync(folderId: "15");

        // Assert
        apiMock.VerifyAll();
    }

    [Test]
    public async Task UploadFileAsync_StorageRejectsUpload_ThrowsWithoutFinalizing()
    {
        // Arrange
        SetupStorageHandler(HttpStatusCode.Forbidden);
        SetupUploadSession();

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(async () =>
            await UploadAsync(folderId: null)
        );

        // Assert
        exception.Message.ShouldContain("Forbidden");
        apiMock.Verify(
            x =>
                x.FinalizeUploadAsync(
                    It.IsAny<string>(),
                    It.IsAny<FinalizeUploadRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task UploadFileAsync_FinalizationTimesOut_ReturnsTheFileCloudFamStillCreated()
    {
        // Arrange
        SetupStorageHandler(HttpStatusCode.OK);
        SetupUploadSession();

        apiMock
            .Setup(x =>
                x.FinalizeUploadAsync(
                    ApiKey,
                    It.IsAny<FinalizeUploadRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                await CreateErrorEnvelopeAsync<FinalizeUploadResponse>(
                    (HttpStatusCode)524,
                    "error code: 524"
                )
            );

        apiMock
            .SetupSequence(x =>
                x.ListFilesAsync(
                    ApiKey,
                    1,
                    100,
                    Path.GetFileName(filePath),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateRawResponse(new FileListResponse(true, [], null)))
            .ReturnsAsync(
                CreateRawResponse(
                    new FileListResponse(
                        Success: true,
                        [
                            new FileListItemResponse(
                                Id: 139476,
                                OriginalFilename: Path.GetFileName(filePath),
                                FileSizeBytes: new FileInfo(filePath).Length,
                                DownloadCount: 0,
                                ShortUrlId: FileCode,
                                DownloadUrl: $"https://cloudfam.io/{FileCode}",
                                Status: "active",
                                UploadedAt: "2026-09-13 20:52:52"
                            ),
                        ],
                        new PaginationResponse(1, 1, 100, 1)
                    )
                )
            );

        // Act
        var result = await UploadAsync(folderId: null);

        // Assert
        result.FileId.ShouldBe("139476");
        result.FileUrl.ShouldBe($"https://cloudfam.io/{FileCode}");
    }

    [Test]
    public async Task UploadFileAsync_FinalizationTimesOutAndFileNeverAppears_Throws()
    {
        // Arrange
        SetupStorageHandler(HttpStatusCode.OK);
        SetupUploadSession();

        apiMock
            .Setup(x =>
                x.FinalizeUploadAsync(
                    ApiKey,
                    It.IsAny<FinalizeUploadRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                await CreateErrorEnvelopeAsync<FinalizeUploadResponse>(
                    (HttpStatusCode)524,
                    "error code: 524"
                )
            );

        apiMock
            .Setup(x =>
                x.ListFilesAsync(
                    ApiKey,
                    1,
                    100,
                    Path.GetFileName(filePath),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateRawResponse(new FileListResponse(true, [], null)));

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(async () =>
            await UploadAsync(folderId: null)
        );

        // Assert
        exception.Message.ShouldContain("524");
    }

    [Test]
    public async Task UploadFileAsync_FinalizationRejected_ThrowsWithoutSearchingForTheFile()
    {
        // Arrange
        SetupStorageHandler(HttpStatusCode.OK);
        SetupUploadSession();

        apiMock
            .Setup(x =>
                x.FinalizeUploadAsync(
                    ApiKey,
                    It.IsAny<FinalizeUploadRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                await CreateErrorEnvelopeAsync<FinalizeUploadResponse>(
                    HttpStatusCode.BadRequest,
                    """{"success":false,"error":{"code":400,"message":"Invalid or missing upload key."}}"""
                )
            );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(async () =>
            await UploadAsync(folderId: null)
        );

        // Assert
        exception.Message.ShouldContain("Invalid or missing upload key.");
        apiMock.Verify(
            x =>
                x.ListFilesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task CreateFolderAsync_ExistingRootFolder_ReturnsExistingIdWithoutCreating()
    {
        // Arrange
        apiMock
            .Setup(x => x.ListFoldersAsync(ApiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateEnvelope(
                    new FolderListResponse(
                        [
                            new FolderResponse(19, "Release.Name", ParentId: 12, TotalFiles: 0),
                            new FolderResponse(12, "release.name", ParentId: null, TotalFiles: 3),
                        ],
                        RootFiles: 0
                    )
                )
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            config,
            "Release.Name",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("12");
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
    public async Task CreateFolderAsync_UnknownFolder_CreatesRootFolder()
    {
        // Arrange
        apiMock
            .Setup(x => x.ListFoldersAsync(ApiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEnvelope(new FolderListResponse([], RootFiles: 0)));

        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    ApiKey,
                    It.Is<CreateFolderRequest>(request =>
                        request.Name == "Release.Name" && request.ParentId == null
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateEnvelope(new CreateFolderResponse(15, "Release.Name", null)));

        // Act
        var result = await apiClient.CreateFolderAsync(
            config,
            "Release.Name",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("15");
    }

    [Test]
    public async Task MoveFileToFolderAsync_WithoutExternalId_ResolvesFileIdFromFileList()
    {
        // Arrange
        apiMock
            .Setup(x => x.ListFilesAsync(ApiKey, 1, 100, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateRawResponse(
                    new FileListResponse(
                        Success: true,
                        [
                            new FileListItemResponse(
                                Id: 1042,
                                OriginalFilename: "archive.part1.rar",
                                FileSizeBytes: 184320,
                                DownloadCount: 3,
                                ShortUrlId: FileCode,
                                DownloadUrl: $"https://cloudfam.io/{FileCode}",
                                Status: "active",
                                UploadedAt: "2026-09-13 20:52:52"
                            ),
                        ],
                        new PaginationResponse(1, 1, 100, 1)
                    )
                )
            );

        apiMock
            .Setup(x =>
                x.MoveFilesAsync(
                    ApiKey,
                    It.Is<MoveFilesRequest>(request => request.FileIds.Single() == 1042),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateEnvelope(new MoveFilesResponse(15, MovedCount: 1)));

        // Act
        await apiClient.MoveFileToFolderAsync(
            config,
            $"https://cloudfam.io/{FileCode}",
            externalId: null,
            folderId: "15",
            CancellationToken.None
        );

        // Assert
        apiMock.VerifyAll();
    }

    [Test]
    public async Task MoveFileToFolderAsync_NothingWasMoved_Throws()
    {
        // Arrange
        apiMock
            .Setup(x =>
                x.MoveFilesAsync(
                    ApiKey,
                    It.IsAny<MoveFilesRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateEnvelope(new MoveFilesResponse(15, MovedCount: 0)));

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(async () =>
            await apiClient.MoveFileToFolderAsync(
                config,
                $"https://cloudfam.io/{FileCode}",
                externalId: "1042",
                folderId: "15",
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("did not move file 1042");
    }

    [Test]
    public async Task CheckLinksAsync_MultipleLinks_ChecksAllCodesInOneRequest()
    {
        // Arrange
        SetupValidApiKey();
        apiMock
            .Setup(x =>
                x.GetFileInfoAsync(ApiKey, $"{FileCode},x923fa", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateRawResponse(
                    new FileInfoResponse(
                        Status: 200,
                        [
                            new FileInfoItemResponse(
                                FileCode,
                                "archive.part1.rar",
                                Status: 200,
                                Size: 184320,
                                Downloads: 12,
                                IsAlive: true,
                                StatusText: "active"
                            ),
                            new FileInfoItemResponse(
                                "x923fa",
                                null,
                                Status: 404,
                                Size: 0,
                                Downloads: 0,
                                IsAlive: false,
                                StatusText: "not_found"
                            ),
                        ],
                        Message: "OK"
                    )
                )
            );

        // Act
        var result = await apiClient.CheckLinksAsync(
            config,
            [
                new FileUrlToCheckDto($"https://cloudfam.io/{FileCode}", ExternalId: null),
                new FileUrlToCheckDto("https://cloudfam.io/x923fa", ExternalId: null),
            ],
            CancellationToken.None
        );

        // Assert
        result[$"https://cloudfam.io/{FileCode}"].IsOnline.ShouldBeTrue();
        result[$"https://cloudfam.io/{FileCode}"].DownloadCount.ShouldBe(12);
        result["https://cloudfam.io/x923fa"].IsOnline.ShouldBeFalse();
        result["https://cloudfam.io/x923fa"].DownloadCount.ShouldBeNull();
    }

    [Test]
    public async Task CheckLinksAsync_InvalidApiKey_Throws()
    {
        // Arrange
        apiMock
            .Setup(x => x.GetProfileAsync(ApiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFailedEnvelope<UserProfileResponse>(HttpStatusCode.Forbidden));

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(async () =>
            await apiClient.CheckLinksAsync(
                config,
                [new FileUrlToCheckDto($"https://cloudfam.io/{FileCode}", ExternalId: null)],
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe("Invalid credentials");
    }

    [Test]
    public async Task CheckLinksAsync_UrlWithoutFileCode_OmitsLink()
    {
        // Arrange
        SetupValidApiKey();

        // Act
        var result = await apiClient.CheckLinksAsync(
            config,
            [new FileUrlToCheckDto("https://cloudfam.io/", ExternalId: null)],
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public void ExtractFileCode_DownloadUrl_ReturnsFileCode()
    {
        // Act
        var result = ApiClient.ExtractFileCode($"https://cloudfam.io/{FileCode}");

        // Assert
        result.ShouldBe(FileCode);
    }

    [Test]
    public void ExtractFileCode_UrlWithHtmlSuffix_ReturnsFileCode()
    {
        // Act
        var result = ApiClient.ExtractFileCode($"https://cloudfam.io/{FileCode}.html");

        // Assert
        result.ShouldBe(FileCode);
    }

    [Test]
    public void ExtractFileCode_UrlWithoutPath_ReturnsNull()
    {
        // Act
        var result = ApiClient.ExtractFileCode("https://cloudfam.io/");

        // Assert
        result.ShouldBeNull();
    }

    private void SetupValidApiKey()
    {
        apiMock
            .Setup(x => x.GetProfileAsync(ApiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateEnvelope(
                    new UserProfileResponse(2688, "bearcat", "active", 0, StorageLimitGb: 300)
                )
            );
    }

    private void SetupUploadSession()
    {
        apiMock
            .Setup(x => x.CreateUploadSessionAsync(ApiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateEnvelope(new UploadSessionResponse(UploadUrl, UploadKey, ExpiresIn: 1800))
            );
    }

    private void SetupFinalizeUpload()
    {
        apiMock
            .Setup(x =>
                x.FinalizeUploadAsync(
                    ApiKey,
                    It.Is<FinalizeUploadRequest>(request =>
                        request.Key == UploadKey
                        && request.OriginalFilename == Path.GetFileName(filePath)
                        && request.FileSize == new FileInfo(filePath).Length
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateEnvelope(
                    new FinalizeUploadResponse(
                        FileId: 139473,
                        Filename: Path.GetFileName(filePath),
                        FileSize: new FileInfo(filePath).Length,
                        DownloadLink: $"https://cloudfam.io/{FileCode}"
                    )
                )
            );
    }

    private StorageTestHttpMessageHandler SetupStorageHandler(HttpStatusCode statusCode)
    {
        var httpMessageHandler = new StorageTestHttpMessageHandler(statusCode);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler));

        return httpMessageHandler;
    }

    private async Task<CloudFamUploadResult> UploadAsync(string? folderId)
    {
        var fileInfo = new FileInfo(filePath);
        await using var stream = File.OpenRead(filePath);

        return await apiClient.UploadFileAsync(
            config: config,
            stream: stream,
            fileName: fileInfo.Name,
            fileSize: fileInfo.Length,
            folderId: folderId,
            cancellationToken: CancellationToken.None
        );
    }

    private static ApiResponse<CloudFamResponse<T>> CreateEnvelope<T>(T content)
    {
        return new ApiResponse<CloudFamResponse<T>>(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(),
            },
            new CloudFamResponse<T>(Success: true, Message: null, Data: content),
            new RefitSettings(),
            error: null
        );
    }

    private static ApiResponse<CloudFamResponse<T>> CreateFailedEnvelope<T>(
        HttpStatusCode statusCode
    )
    {
        return new ApiResponse<CloudFamResponse<T>>(
            new HttpResponseMessage(statusCode) { RequestMessage = new HttpRequestMessage() },
            new CloudFamResponse<T>(Success: false, Message: "Unauthorized", Data: default),
            new RefitSettings(),
            error: null
        );
    }

    private static async Task<ApiResponse<CloudFamResponse<T>>> CreateErrorEnvelopeAsync<T>(
        HttpStatusCode statusCode,
        string content
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://cloudfam.io/api/v3/test");
        var response = new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = new StringContent(content),
        };
        var settings = new RefitSettings();
        var error = await ApiException.Create(request, HttpMethod.Post, response, settings);

        return new ApiResponse<CloudFamResponse<T>>(response, default!, settings, error);
    }

    private static ApiResponse<T> CreateRawResponse<T>(T content)
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(),
            },
            content,
            new RefitSettings(),
            error: null
        );
    }

    private sealed class StorageTestHttpMessageHandler(HttpStatusCode statusCode)
        : HttpMessageHandler
    {
        public long UploadedBytes { get; private set; }

        public HttpMethod? Method { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Method = request.Method;
            UploadedBytes = (
                await request.Content!.ReadAsByteArrayAsync(cancellationToken)
            ).LongLength;

            return new HttpResponseMessage(statusCode);
        }
    }
}
