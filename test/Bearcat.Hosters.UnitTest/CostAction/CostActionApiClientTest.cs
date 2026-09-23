using System.Net;
using System.Text;
using System.Text.Json;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.CostAction.Api;
using Bearcat.Hosters.Shared.CostAction.Api.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.CostAction;

public class CostActionApiClientTest
{
    private const string ApiKey = "costaction-api-key";

    private const string AppType = "fd1";

    private const string FileId = "cye2wolk2zz7";

    private const string UploadUrl = "https://s329.turbobit.net/v2/file/token";

    private const string FileUrl = $"https://trbt.cc/{FileId}.html";

    private const string RateLimitContent =
        """{"x-ratelimit-limit":60,"x-ratelimit-remaining":0,"retry-after":9,"message":"Too many attempts","status":"error"}""";

    private Mock<ICostActionApi> apiMock = null!;
    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private CostActionApiClient apiClient = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        apiMock = new Mock<ICostActionApi>(MockBehavior.Strict);
        httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<CostActionApiClient>>();

        apiClient = new CostActionApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        )
        {
            MaximumRateLimitDelay = TimeSpan.Zero,
        };

        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        await File.WriteAllTextAsync(filePath, "costaction test content");
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_NewFile_PostsMultipartToUploadUrlAndReturnsShareLink()
    {
        // Arrange
        var storageHandler = SetupStorageHandler(
            HttpStatusCode.OK,
            $$"""{"message":"Everything is ok","id":"{{FileId}}","delete_id":"39A5","status":"success"}"""
        );
        SetupUploadUrl();
        SetupShareLinks();

        // Act
        var result = await UploadAsync(folderId: null);

        // Assert
        result.FileId.ShouldBe(FileId);
        result.FileUrl.ShouldBe(FileUrl);
        storageHandler.Method.ShouldBe(HttpMethod.Post);
        storageHandler.RequestUri.ShouldBe(new Uri(UploadUrl));
        storageHandler.Body.ShouldContain("name=\"file\"");
        storageHandler.Body.ShouldContain("costaction test content");
        apiMock.VerifyAll();
    }

    [Test]
    public async Task UploadFileAsync_WithFolder_UploadsToInboxAndMovesFileIntoFolder()
    {
        // Arrange
        SetupStorageHandler(
            HttpStatusCode.OK,
            $$"""{"message":"Everything is ok","id":"{{FileId}}","status":"success"}"""
        );
        SetupUploadUrl();
        SetupShareLinks();
        SetupMove(
            $$$"""{"files":{"{{{FileId}}}":{"id":"{{{FileId}}}","folder_id":2479849}},"errors":[],"status":"success"}"""
        );

        // Act
        var result = await UploadAsync(folderId: "2479849");

        // Assert
        result.FileId.ShouldBe(FileId);
        apiMock.VerifyAll();
    }

    [Test]
    public async Task UploadFileAsync_StorageQueuesFileWithoutId_Throws()
    {
        // Arrange
        SetupStorageHandler(
            HttpStatusCode.OK,
            """{"message":"File added to the registration queue","status":"success"}"""
        );
        SetupUploadUrl();

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            UploadAsync(folderId: null)
        );

        // Assert
        exception.Message.ShouldContain("without returning a file id");
    }

    [Test]
    public async Task UploadFileAsync_StorageRejectsUpload_ThrowsWithMessage()
    {
        // Arrange
        SetupStorageHandler(
            HttpStatusCode.BadRequest,
            """{"message":"Wrong upload url","status":"error"}"""
        );
        SetupUploadUrl();

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            UploadAsync(folderId: null)
        );

        // Assert
        exception.Message.ShouldContain("Wrong upload url");
    }

    [Test]
    public async Task UploadFileAsync_RateLimitedOnce_RetriesAndSucceeds()
    {
        // Arrange
        SetupStorageHandler(
            HttpStatusCode.OK,
            $$"""{"message":"Everything is ok","id":"{{FileId}}","status":"success"}"""
        );
        var rateLimited = await CreateErrorResponseAsync<UploadUrlResponse>(
            HttpStatusCode.TooManyRequests,
            RateLimitContent
        );
        apiMock
            .SetupSequence(x =>
                x.GetUploadUrlAsync(
                    ApiKey,
                    It.IsAny<UploadUrlRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(rateLimited)
            .ReturnsAsync(CreateResponse(new UploadUrlResponse("success", null, UploadUrl, null)));
        SetupShareLinks();

        // Act
        var result = await UploadAsync(folderId: null);

        // Assert
        result.FileUrl.ShouldBe(FileUrl);
        apiMock.Verify(
            x =>
                x.GetUploadUrlAsync(
                    ApiKey,
                    It.IsAny<UploadUrlRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [Test]
    public async Task UploadFileAsync_RateLimitNeverLifts_ThrowsAfterAllAttempts()
    {
        // Arrange
        apiMock
            .Setup(x =>
                x.GetUploadUrlAsync(
                    ApiKey,
                    It.IsAny<UploadUrlRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
                CreateErrorResponseAsync<UploadUrlResponse>(
                    HttpStatusCode.TooManyRequests,
                    RateLimitContent
                )
            );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            UploadAsync(folderId: null)
        );

        // Assert
        exception.Message.ShouldContain("Too many attempts");
        apiMock.Verify(
            x =>
                x.GetUploadUrlAsync(
                    ApiKey,
                    It.IsAny<UploadUrlRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(5)
        );
    }

    [Test]
    public async Task CheckFilesAsync_OnlineDeletedAndMissingFiles_ReturnsStatusPerFileId()
    {
        // Arrange
        apiMock
            .Setup(x =>
                x.GetFilesInfoAsync(
                    ApiKey,
                    "download_count",
                    100,
                    It.Is<FilesInfoRequest>(request =>
                        request.AppType == AppType
                        && request.FileIds.SequenceEqual(new[] { "online", "deleted", "missing" })
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateResponse(
                    new FilesInfoResponse(
                        "success",
                        null,
                        [
                            new FileInfoResponse(
                                "online",
                                "a.rar",
                                IsDeleted: false,
                                DownloadCount: 7
                            ),
                            new FileInfoResponse(
                                "deleted",
                                "b.rar",
                                IsDeleted: true,
                                DownloadCount: 3
                            ),
                        ]
                    )
                )
            );

        // Act
        var result = await apiClient.CheckFilesAsync(
            ApiKey,
            AppType,
            ["online", "deleted", "missing"],
            CancellationToken.None
        );

        // Assert
        result["online"].ShouldBe(new LinkCheckStatus(IsOnline: true, DownloadCount: 7));
        result["deleted"].ShouldBe(new LinkCheckStatus(IsOnline: false, DownloadCount: null));
        result["missing"].ShouldBe(new LinkCheckStatus(IsOnline: false, DownloadCount: null));
    }

    [Test]
    public async Task CheckFilesAsync_RequestFails_Throws()
    {
        // Arrange
        var unauthorized = await CreateErrorResponseAsync<FilesInfoResponse>(
            HttpStatusCode.Unauthorized,
            """{"message":"This action is unauthorized.","status":"error"}"""
        );
        apiMock
            .Setup(x =>
                x.GetFilesInfoAsync(
                    ApiKey,
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<FilesInfoRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(unauthorized);

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.CheckFilesAsync(ApiKey, AppType, [FileId], CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldContain("This action is unauthorized.");
    }

    [Test]
    public async Task CreateFolderAsync_RootFolderExists_ReturnsExistingIdWithoutCreating()
    {
        // Arrange
        SetupFolderSearch(
            "Release",
            new FolderResponse(1, "Release.Sub", ParentFolderId: null),
            new FolderResponse(2, "release", ParentFolderId: 1),
            new FolderResponse(3, "RELEASE", ParentFolderId: null)
        );

        // Act
        var folderId = await apiClient.CreateFolderAsync(
            ApiKey,
            AppType,
            "Release",
            CancellationToken.None
        );

        // Assert
        folderId.ShouldBe("3");
        apiMock.Verify(
            x =>
                x.CreateFolderAsync(
                    It.IsAny<string>(),
                    It.IsAny<FolderCreateRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task CreateFolderAsync_FolderMissing_CreatesRootFolder()
    {
        // Arrange
        SetupFolderSearch("Release");
        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    ApiKey,
                    new FolderCreateRequest(
                        "Release",
                        CanBeCopied: false,
                        AppType,
                        ParentFolderId: null
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateResponse(
                    new FolderCreateResponse(
                        "success",
                        null,
                        new FolderResponse(2479849, "Release", ParentFolderId: null)
                    ),
                    HttpStatusCode.Created
                )
            );

        // Act
        var folderId = await apiClient.CreateFolderAsync(
            ApiKey,
            AppType,
            "Release",
            CancellationToken.None
        );

        // Assert
        folderId.ShouldBe("2479849");
    }

    [Test]
    public async Task CreateFolderAsync_FolderCreatedConcurrently_ReturnsFolderFoundAfterConflict()
    {
        // Arrange
        apiMock
            .SetupSequence(x =>
                x.SearchFoldersAsync(
                    ApiKey,
                    new FolderSearchRequest(AppType, "Release"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateResponse(new FolderSearchResponse("success", null, [])))
            .ReturnsAsync(
                CreateResponse(
                    new FolderSearchResponse(
                        "success",
                        null,
                        [new FolderResponse(42, "Release", ParentFolderId: null)]
                    )
                )
            );
        var conflict = await CreateErrorResponseAsync<FolderCreateResponse>(
            HttpStatusCode.UnprocessableEntity,
            """{"message":"Name of the folder should be unique","status":"error"}"""
        );
        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    ApiKey,
                    It.IsAny<FolderCreateRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(conflict);

        // Act
        var folderId = await apiClient.CreateFolderAsync(
            ApiKey,
            AppType,
            "Release",
            CancellationToken.None
        );

        // Assert
        folderId.ShouldBe("42");
    }

    [Test]
    public async Task MoveFileToFolderAsync_FileMoved_Succeeds()
    {
        // Arrange
        SetupMove(
            $$$"""{"files":{"{{{FileId}}}":{"id":"{{{FileId}}}","folder_id":2479849}},"errors":[],"status":"success"}"""
        );

        // Act
        await apiClient.MoveFileToFolderAsync(ApiKey, FileId, "2479849", CancellationToken.None);

        // Assert
        apiMock.VerifyAll();
    }

    [Test]
    public async Task MoveFileToFolderAsync_ApiReportsFileError_Throws()
    {
        // Arrange
        SetupMove(
            $$"""{"files":[],"errors":{"{{FileId}}":"File not found or wrong ID"},"status":"success"}"""
        );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.MoveFileToFolderAsync(ApiKey, FileId, "2479849", CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldContain("File not found or wrong ID");
    }

    [Test]
    public async Task VerifyAccessAsync_AppNotActivated_ThrowsWithApiMessage()
    {
        // Arrange
        var notActivated = await CreateErrorResponseAsync<FolderSearchResponse>(
            HttpStatusCode.BadRequest,
            """{"message":"fd2 user does not exist","status":"error"}"""
        );
        apiMock
            .Setup(x =>
                x.SearchFoldersAsync(
                    ApiKey,
                    new FolderSearchRequest("fd2", Name: null),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(notActivated);

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.VerifyAccessAsync(ApiKey, "fd2", CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldContain("fd2 user does not exist");
    }

    private void SetupUploadUrl()
    {
        apiMock
            .Setup(x =>
                x.GetUploadUrlAsync(
                    ApiKey,
                    new UploadUrlRequest(AppType),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateResponse(
                    new UploadUrlResponse("success", null, UploadUrl, "2026-09-23 12:32:56")
                )
            );
    }

    private void SetupShareLinks()
    {
        apiMock
            .Setup(x => x.GetFileShareLinksAsync(ApiKey, FileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateResponse(
                    new FileShareResponse(
                        "success",
                        null,
                        new FileShareLinksResponse(
                            new FileShareLinkVariantsResponse(
                                FileUrl,
                                $"https://trbt.cc/{FileId}/file.bin.html"
                            )
                        )
                    )
                )
            );
    }

    private void SetupFolderSearch(string folderName, params FolderResponse[] folders)
    {
        apiMock
            .Setup(x =>
                x.SearchFoldersAsync(
                    ApiKey,
                    new FolderSearchRequest(AppType, folderName),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateResponse(new FolderSearchResponse("success", null, folders)));
    }

    private void SetupMove(string json)
    {
        var response = JsonSerializer.Deserialize<FilesMoveResponse>(json)!;

        apiMock
            .Setup(x =>
                x.MoveFilesAsync(
                    ApiKey,
                    It.Is<FilesMoveRequest>(request =>
                        request.FolderId == "2479849" && request.Ids.Single() == FileId
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateResponse(response));
    }

    private StorageTestHttpMessageHandler SetupStorageHandler(
        HttpStatusCode statusCode,
        string responseContent
    )
    {
        var httpMessageHandler = new StorageTestHttpMessageHandler(statusCode, responseContent);
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler));

        return httpMessageHandler;
    }

    private async Task<CostActionUploadResult> UploadAsync(string? folderId)
    {
        var fileInfo = new FileInfo(filePath);
        await using var stream = File.OpenRead(filePath);

        return await apiClient.UploadFileAsync(
            apiKey: ApiKey,
            appType: AppType,
            stream: stream,
            fileName: fileInfo.Name,
            fileSize: fileInfo.Length,
            folderId: folderId,
            cancellationToken: CancellationToken.None
        );
    }

    private static ApiResponse<T> CreateResponse<T>(
        T content,
        HttpStatusCode statusCode = HttpStatusCode.OK
    )
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(statusCode) { RequestMessage = new HttpRequestMessage() },
            content,
            new RefitSettings(),
            error: null
        );
    }

    private static async Task<ApiResponse<T>> CreateErrorResponseAsync<T>(
        HttpStatusCode statusCode,
        string content
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.costaction.com/v1/test");
        var response = new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = new StringContent(content),
        };
        var settings = new RefitSettings();
        var error = await ApiException.Create(request, HttpMethod.Post, response, settings);

        return new ApiResponse<T>(response, default, settings, error);
    }

    private sealed class StorageTestHttpMessageHandler(
        HttpStatusCode statusCode,
        string responseContent
    ) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Body = Encoding.UTF8.GetString(
                await request.Content!.ReadAsByteArrayAsync(cancellationToken)
            );

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent),
            };
        }
    }
}
