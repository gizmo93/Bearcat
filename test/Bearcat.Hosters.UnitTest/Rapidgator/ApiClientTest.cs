using System.Net;
using Bearcat.Hosters.Rapidgator;
using Bearcat.Hosters.Rapidgator.Api;
using Bearcat.Hosters.Rapidgator.Api.File;
using Bearcat.Hosters.Rapidgator.Api.Folder;
using Bearcat.Hosters.Rapidgator.Api.User;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;
using RapidgatorApiClient = Bearcat.Hosters.Rapidgator.Api.ApiClient;

namespace Bearcat.Hosters.UnitTest.Rapidgator;

public class ApiClientTest
{
    private Mock<IRapidgatorApi> apiMock = null!;
    private RapidgatorApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IRapidgatorApi>(MockBehavior.Strict);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<ILogger<RapidgatorApiClient>>();

        apiClient = new RapidgatorApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        );
    }

    [Test]
    public async Task CreateFolderAsync_ExistingRootFolderWithName_ReturnsExistingFolderId()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };
        SetupLogin();
        apiMock
            .Setup(x => x.GetFolderInfoAsync("token", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateFolderResponse(
                    new FolderResponse.Folder
                    {
                        FolderId = "root-folder-id",
                        Name = "root",
                        Folders =
                        [
                            new FolderResponse.Folder
                            {
                                FolderId = "folder-id",
                                Name = "release-folder",
                                Created = 1,
                            },
                        ],
                    }
                )
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            "release-folder",
            config,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("folder-id");
        apiMock.Verify(
            x =>
                x.CreateFolderAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task CreateFolderAsync_NoExistingRootFolderWithName_CreatesFolder()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };
        SetupLogin();
        apiMock
            .Setup(x => x.GetFolderInfoAsync("token", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateFolderResponse(
                    new FolderResponse.Folder
                    {
                        FolderId = "root-folder-id",
                        Name = "root",
                        Folders =
                        [
                            new FolderResponse.Folder
                            {
                                FolderId = "other-folder-id",
                                Name = "other-folder",
                            },
                        ],
                    }
                )
            );
        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    "token",
                    "release-folder",
                    "root-folder-id",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateFolderResponse(
                    new FolderResponse.Folder
                    {
                        FolderId = "created-folder-id",
                        Name = "release-folder",
                    }
                )
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            "release-folder",
            config,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("created-folder-id");
    }

    [Test]
    public async Task RequestUploadFileAsync_FolderId_PassesFolderIdToApi()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };
        var expectedResponse = new UploadFileResponse
        {
            Status = (int)HttpStatusCode.OK,
            Response = new UploadFileResponse.ResponseObject
            {
                Upload = new UploadFileResponse.Upload { UploadId = "upload-id" },
            },
        };

        SetupLogin();
        apiMock
            .Setup(x =>
                x.RequestUploadFileAsync(
                    "token",
                    "archive.part1.rar",
                    1024,
                    "hash",
                    "folder-id",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await apiClient.RequestUploadFileAsync(
            name: "archive.part1.rar",
            size: 1024,
            hash: "hash",
            folderId: "folder-id",
            config: config,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBeSameAs(expectedResponse);
    }

    private void SetupLogin()
    {
        apiMock
            .Setup(x => x.LoginAsync("user", "password", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    new LoginResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Response = new LoginResponse.ResponseObject { Token = "token" },
                    }
                )
            );
    }

    private static FolderResponse CreateFolderResponse(FolderResponse.Folder folder)
    {
        return new FolderResponse
        {
            Status = (int)HttpStatusCode.OK,
            Response = new FolderResponse.ResponseObject { Folder = folder },
        };
    }

    private static ApiResponse<T> CreateApiResponse<T>(T content)
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(HttpStatusCode.OK),
            content,
            new RefitSettings(),
            error: null
        );
    }
}
