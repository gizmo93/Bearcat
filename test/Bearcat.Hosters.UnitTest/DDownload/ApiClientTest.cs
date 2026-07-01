using System.Net;
using System.Text.Json;
using Bearcat.Hosters.DDownload.Api;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.XFilesharing.Api;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.DDownload;

public class ApiClientTest
{
    private Mock<IDDownloadApi> apiMock = null!;
    private ApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IDDownloadApi>(MockBehavior.Strict);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();

        apiClient = new ApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object)
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
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
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
        var response = JsonSerializer.Deserialize<FileCheckResponse>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

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

    [Test]
    public async Task SetFileFolderAsync_ApiReturnsOk_Completes()
    {
        // Arrange
        var xfilesharingApiMock = apiMock.As<IXFilesharingApi>();
        xfilesharingApiMock
            .Setup(x =>
                x.SetFileFolderAsync(
                    "api-key",
                    "file-code",
                    "folder-id",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new StatusResponse { Status = (int)HttpStatusCode.OK });

        // Act
        await apiClient.SetFileFolderAsync(
            "api-key",
            "file-code",
            "folder-id",
            CancellationToken.None
        );

        // Assert
        xfilesharingApiMock.Verify(
            x =>
                x.SetFileFolderAsync(
                    "api-key",
                    "file-code",
                    "folder-id",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task SetFilePropertiesAsync_PremiumOnly_PassesNumericPremiumOnlyFlagToApi()
    {
        // Arrange
        var xfilesharingApiMock = apiMock.As<IXFilesharingApi>();
        xfilesharingApiMock
            .Setup(x =>
                x.SetFilePropertiesAsync("api-key", "file-code", 1, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new StatusResponse { Status = (int)HttpStatusCode.OK });

        // Act
        await apiClient.SetFilePropertiesAsync(
            "api-key",
            "file-code",
            premiumOnly: true,
            CancellationToken.None
        );

        // Assert
        xfilesharingApiMock.Verify(
            x => x.SetFilePropertiesAsync("api-key", "file-code", 1, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
