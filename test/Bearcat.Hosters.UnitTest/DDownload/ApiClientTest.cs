using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.DDownload.Api;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.DDownload;

public class ApiClientTest
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private Mock<IDDownloadApi> apiMock = null!;
    private ApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IDDownloadApi>(MockBehavior.Strict);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var httpClientProvider = new HttpClientProvider(httpClientFactoryMock.Object);

        apiClient = new ApiClient(
            apiMock.Object,
            httpClientProvider,
            new HosterFileDownloader(httpClientProvider)
        )
        {
            RateLimitRetryDelay = TimeSpan.Zero,
        };
    }

    [Test]
    public async Task RequestUploadAsync_TooManyRequestsThenSuccess_RetriesAndReturnsUploadServer()
    {
        // Arrange
        var uploadServer = new RequestUploadResponse
        {
            Status = (int)HttpStatusCode.OK,
            UploadUrl = "https://upload.example/upload",
            SessionId = "session-id",
        };

        var xfilesharingApiMock = apiMock.As<IXFilesharingApi>();

        xfilesharingApiMock
            .SetupSequence(x => x.RequestUploadAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse<RequestUploadResponse>(null, HttpStatusCode.TooManyRequests)
            )
            .ReturnsAsync(
                CreateApiResponse<RequestUploadResponse>(uploadServer, HttpStatusCode.OK)
            );

        // Act
        var result = await apiClient.RequestUploadAsync("api-key", CancellationToken.None);

        // Assert
        result.ShouldBe(uploadServer);
        xfilesharingApiMock.Verify(
            x => x.RequestUploadAsync("api-key", It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [TestCase(
        """{"msg":"OK","result":{"files":[],"folders":[]},"status":200}""",
        HosterFolderContentState.Empty
    )]
    [TestCase(
        """{"msg":"OK","result":{"files":[{"file_code":"oejag2gpfcr0","fld_id":208401,"link":"https://ddownload.com/oejag2gpfcr0","name":"part01.rar","uploaded":"2026-11-20 10:00:00"},{"file_code":"ni6z27dyros2","fld_id":"208401","link":"https://ddownload.com/ni6z27dyros2","name":"part02.rar","uploaded":"2026-11-20 10:01:00"}],"folders":[]},"status":200}""",
        HosterFolderContentState.NotEmpty
    )]
    [TestCase(
        """{"msg":"OK","result":{"files":[],"folders":[{"code":null,"fld_id":1037117,"name":"sub-folder"}]},"status":200}""",
        HosterFolderContentState.NotEmpty
    )]
    public async Task GetFolderContentStateAsync_FolderListResponse_MapsFilesAndSubFoldersToState(
        string json,
        HosterFolderContentState expectedState
    )
    {
        // Arrange
        apiMock
            .As<IXFilesharingApi>()
            .Setup(x => x.GetFolderListAsync("api-key", "208401", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                JsonSerializer.Deserialize<FolderListResponse>(json, JsonSerializerOptions)!
            );

        // Act
        var result = await apiClient.GetFolderContentStateAsync(
            apiKey: "api-key",
            folderId: "208401",
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe(expectedState);
    }

    [TestCase(
        """{"msg":"OK","result":{"deleted_folder":"name","files_moved_to_root":0,"subfolders_moved_to_parent":0},"status":200}"""
    )]
    [TestCase("""{"msg":"Folder not found","status":404}""")]
    public async Task DeleteFolderAsync_SuccessOrFolderNotFound_ReturnsWithoutThrowing(string json)
    {
        // Arrange
        apiMock
            .As<IXFilesharingApi>()
            .Setup(x => x.DeleteFolderAsync("api-key", "208401", It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Deserialize<StatusResponse>(json, JsonSerializerOptions)!);

        // Act
        await apiClient.DeleteFolderAsync("api-key", "208401", CancellationToken.None);

        // Assert
        apiMock
            .As<IXFilesharingApi>()
            .Verify(
                x => x.DeleteFolderAsync("api-key", "208401", It.IsAny<CancellationToken>()),
                Times.Once
            );
    }

    [Test]
    public async Task DeleteFolderAsync_InvalidOperation_ThrowsWithMessage()
    {
        // Arrange
        apiMock
            .As<IXFilesharingApi>()
            .Setup(x => x.DeleteFolderAsync("api-key", "208401", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                JsonSerializer.Deserialize<StatusResponse>(
                    """{"msg":"Invalid operation","status":400}""",
                    JsonSerializerOptions
                )!
            );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.DeleteFolderAsync("api-key", "208401", CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldContain("Invalid operation");
    }

    private static ApiResponse<T> CreateApiResponse<T>(T? content, HttpStatusCode statusCode)
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(statusCode) { RequestMessage = new HttpRequestMessage() },
            content,
            new RefitSettings(),
            error: null
        );
    }

    [Test]
    public void FolderCreateResponse_NumericFolderId_DeserializesFolderIdAsString()
    {
        // Arrange
        const string json = """
            {
              "msg": "OK",
              "result": {
                "fld_id": 1037116
              },
              "status": 200
            }
            """;

        // Act
        var response = JsonSerializer.Deserialize<FolderCreateResponse>(
            json,
            JsonSerializerOptions
        );

        // Assert
        response.ShouldNotBeNull();
        response.Result.ShouldNotBeNull();
        response.Result.FolderId.ShouldBe("1037116");
    }

    [Test]
    public void FileCheckResponse_RealResponse_DeserializesFilesWithDownloads()
    {
        // Arrange
        const string json = """
            {
              "msg": "OK",
              "result": {
                "files": [
                  {
                    "downloads": 0,
                    "file_code": "oejag2gpfcr0",
                    "name": "part58.rar",
                    "size": "105906178",
                    "status": 200,
                    "uploaded": "2026-05-31 19:39:51"
                  },
                  {
                    "file_code": "ni6z27dyros2",
                    "msg": "Not found",
                    "status": 404
                  }
                ],
                "stats": { "dmca": 0, "found": 1, "not_found": 1 },
                "total": 2
              },
              "status": 200
            }
            """;

        // Act
        var response = JsonSerializer.Deserialize<FileCheckResponse>(json, JsonSerializerOptions);

        // Assert
        response.ShouldNotBeNull();
        response.Result.Files.Length.ShouldBe(2);
        response.Result.Files[0].FileCode.ShouldBe("oejag2gpfcr0");
        response.Result.Files[0].Downloads.ShouldBe("0");
        response.Result.Files[1].FileCode.ShouldBe("ni6z27dyros2");
        response.Result.Files[1].Downloads.ShouldBeNull();
    }

    [Test]
    public async Task FilesExistAsync_ApiReturnsFileCheckResults_MapsExistenceAndDownloadCount()
    {
        // Arrange
        apiMock
            .Setup(x =>
                x.CheckFilesAsync("api-key", It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new FileCheckResponse
                {
                    Msg = "OK",
                    Status = (int)HttpStatusCode.OK,
                    Result = new FileCheckResult
                    {
                        Files =
                        [
                            new FileCheckFile
                            {
                                FileCode = "online-code",
                                Status = (int)HttpStatusCode.OK,
                                Downloads = "150",
                            },
                            new FileCheckFile
                            {
                                FileCode = "not-found-code",
                                Status = (int)HttpStatusCode.NotFound,
                            },
                            new FileCheckFile
                            {
                                FileCode = "dmca-code",
                                Status = (int)HttpStatusCode.UnavailableForLegalReasons,
                            },
                        ],
                    },
                }
            );

        // Act
        var result = await apiClient.FilesExistAsync(
            "api-key",
            new HashSet<string> { "online-code", "not-found-code", "dmca-code" },
            CancellationToken.None
        );

        // Assert
        result["online-code"].Exists.ShouldBeTrue();
        result["online-code"].DownloadCount.ShouldBe(150);
        result["not-found-code"].Exists.ShouldBeFalse();
        result["not-found-code"].DownloadCount.ShouldBeNull();
        result["dmca-code"].Exists.ShouldBeFalse();
        result["dmca-code"].DownloadCount.ShouldBeNull();
    }

    [Test]
    public async Task FilesExistAsync_ApiRejectsKey_ThrowsHosterCredentialsRejected()
    {
        // Arrange
        var response = JsonSerializer.Deserialize<FileCheckResponse>(
            """{"status":400,"msg":"Invalid key","server_time":"2026-09-30 10:12:44"}"""
        )!;
        apiMock
            .Setup(x =>
                x.CheckFilesAsync("api-key", It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(response);

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            apiClient.FilesExistAsync(
                "api-key",
                new HashSet<string> { "file-code" },
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe("Invalid key");
    }

    [Test]
    public async Task CreateFolderAsync_ExistingRootFolderWithName_ReturnsExistingFolderId()
    {
        // Arrange
        var xfilesharingApiMock = apiMock.As<IXFilesharingApi>();
        xfilesharingApiMock
            .Setup(x => x.GetFolderListAsync("api-key", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new FolderListResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Result = new FolderListResponse.ResultObject
                    {
                        Folders =
                        [
                            new FolderListResponse.Folder
                            {
                                FolderId = "folder-id",
                                Name = "release-folder",
                            },
                        ],
                    },
                }
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            "api-key",
            "release-folder",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("folder-id");
        xfilesharingApiMock.Verify(
            x =>
                x.CreateFolderAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task CreateFolderAsync_NoExistingRootFolderWithName_CreatesFolder()
    {
        // Arrange
        var xfilesharingApiMock = apiMock.As<IXFilesharingApi>();
        xfilesharingApiMock
            .Setup(x => x.GetFolderListAsync("api-key", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new FolderListResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Result = new FolderListResponse.ResultObject
                    {
                        Folders =
                        [
                            new FolderListResponse.Folder
                            {
                                FolderId = "other-folder-id",
                                Name = "other-folder",
                            },
                        ],
                    },
                }
            );
        xfilesharingApiMock
            .Setup(x =>
                x.CreateFolderAsync("api-key", "release-folder", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new FolderCreateResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Result = new FolderCreateResponse.ResultObject
                    {
                        FolderId = "created-folder-id",
                    },
                }
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            "api-key",
            "release-folder",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("created-folder-id");
    }
}
