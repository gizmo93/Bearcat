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
                x.CreateFolderAsync(
                    "api-key",
                    "release-folder",
                    It.IsAny<CancellationToken>()
                )
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
                x.SetFilePropertiesAsync(
                    "api-key",
                    "file-code",
                    1,
                    It.IsAny<CancellationToken>()
                )
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
            x =>
                x.SetFilePropertiesAsync(
                    "api-key",
                    "file-code",
                    1,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
