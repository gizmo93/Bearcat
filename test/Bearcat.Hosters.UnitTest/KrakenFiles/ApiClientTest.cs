using System.Net;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.KrakenFiles;
using Bearcat.Hosters.KrakenFiles.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.KrakenFiles;

public class ApiClientTest
{
    private Mock<IKrakenFilesApi> apiMock = null!;
    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private ApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IKrakenFilesApi>(MockBehavior.Strict);
        httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<ApiClient>>();

        apiClient = new ApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        );
    }

    [Test]
    public async Task CreateFolderAsync_ExistingRootFolderWithName_ReturnsExistingFolderId()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };
        apiMock
            .Setup(x => x.ListFoldersAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new FolderListResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Data =
                    [
                        new FolderData
                        {
                            Id = "child-folder-id",
                            Name = "release-folder",
                            ParentId = "parent-folder-id",
                        },
                        new FolderData
                        {
                            Id = "root-folder-id",
                            Name = "release-folder",
                            ParentId = null,
                        },
                    ],
                }
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            config,
            "release-folder",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("root-folder-id");
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
    public async Task CreateFolderAsync_NoExistingRootFolderWithName_CreatesFolderAndReturnsListedId()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };
        apiMock
            .SetupSequence(x => x.ListFoldersAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new FolderListResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Data =
                    [
                        new FolderData
                        {
                            Id = "child-folder-id",
                            Name = "release-folder",
                            ParentId = "parent-folder-id",
                        },
                    ],
                }
            )
            .ReturnsAsync(
                new FolderListResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Data =
                    [
                        new FolderData
                        {
                            Id = "created-folder-id",
                            Name = "release-folder",
                            ParentId = null,
                        },
                    ],
                }
            );
        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    "api-key",
                    It.Is<CreateFolderRequest>(request => request.Name == "release-folder"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FolderCreateResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Data = new FolderCreateData { Message = "Folder created successfully" },
                }
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            config,
            "release-folder",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("created-folder-id");
    }

    [Test]
    public async Task EnsureApiKeyAcceptedAsync_FileNotFoundForProbeHash_Completes()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.GetFileAsync("bearcat-login-check", "api-key", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateApiResponse<FileResponse>(HttpStatusCode.NotFound));

        // Act
        var action = () => apiClient.EnsureApiKeyAcceptedAsync(config, CancellationToken.None);

        // Assert
        await action.ShouldNotThrowAsync();
    }

    [Test]
    public async Task EnsureApiKeyAcceptedAsync_InvalidCredentialsForProbeHash_ThrowsHosterCredentialsRejected()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.GetFileAsync("bearcat-login-check", "api-key", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                await CreateErrorApiResponseAsync<FileResponse>(
                    HttpStatusCode.Unauthorized,
                    """{"message":"Invalid credentials."}"""
                )
            );

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            apiClient.EnsureApiKeyAcceptedAsync(config, CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldBe("Invalid credentials.");
    }

    [Test]
    public async Task EnsureApiKeyAcceptedAsync_ServerErrorForProbeHash_ThrowsHttpRequestException()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.GetFileAsync("bearcat-login-check", "api-key", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                await CreateErrorApiResponseAsync<FileResponse>(
                    HttpStatusCode.BadGateway,
                    "<html>Bad Gateway</html>"
                )
            );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.EnsureApiKeyAcceptedAsync(config, CancellationToken.None)
        );

        // Assert
        exception.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
    }

    [Test]
    public async Task CheckLinksAsync_FileCheckThrows_OmitsLinkInsteadOfMarkingOffline()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };
        var fileUrl = "https://krakenfiles.com/view/file-hash/archive.rar.html";

        apiMock
            .Setup(x =>
                x.GetFileAsync("bearcat-login-check", "api-key", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateApiResponse<FileResponse>(HttpStatusCode.NotFound));

        apiMock
            .Setup(x => x.GetFileAsync("file-hash", "api-key", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));

        // Act
        var result = await apiClient.CheckLinksAsync(config, [fileUrl], CancellationToken.None);

        // Assert
        result.ShouldNotContainKey(fileUrl);
    }

    [Test]
    public async Task CheckLinksAsync_FileReportsDownloads_MapsOnlineStatusAndDownloadCount()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };
        var fileUrl = "https://krakenfiles.com/view/file-hash/archive.rar.html";

        apiMock
            .Setup(x =>
                x.GetFileAsync("bearcat-login-check", "api-key", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateApiResponse<FileResponse>(HttpStatusCode.NotFound));

        apiMock
            .Setup(x => x.GetFileAsync("file-hash", "api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new FileResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Data = new FileData { Downloads = 23 },
                    }
                )
            );

        // Act
        var result = await apiClient.CheckLinksAsync(config, [fileUrl], CancellationToken.None);

        // Assert
        result[fileUrl].IsOnline.ShouldBeTrue();
        result[fileUrl].DownloadCount.ShouldBe(23);
    }

    [Test]
    public async Task GetFolderContentStatesAsync_FoldersWithFilesAndSubFolder_MapsStatesAndOmitsFailedAndUnlistedFolders()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x => x.ListFoldersAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new FolderListResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Data =
                    [
                        new FolderData { Id = "empty-folder-id", Name = "Empty.Release" },
                        new FolderData { Id = "files-folder-id", Name = "Files.Release" },
                        new FolderData { Id = "parent-folder-id", Name = "Parent.Release" },
                        new FolderData
                        {
                            Id = "child-folder-id",
                            Name = "Child",
                            ParentId = "parent-folder-id",
                        },
                        new FolderData { Id = "failing-folder-id", Name = "Failing.Release" },
                    ],
                }
            );

        SetupFolderFileList("empty-folder-id", totalItems: 0);
        SetupFolderFileList("files-folder-id", totalItems: 4);

        apiMock
            .Setup(x =>
                x.ListFilesAsync(
                    "api-key",
                    "failing-folder-id",
                    1,
                    1,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("network down"));

        // Act
        var result = await apiClient.GetFolderContentStatesAsync(
            config: config,
            folderIds:
            [
                "empty-folder-id",
                "files-folder-id",
                "parent-folder-id",
                "failing-folder-id",
                "unlisted-folder-id",
            ],
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new HosterFolderContentStateDto("empty-folder-id", HosterFolderContentState.Empty),
            new HosterFolderContentStateDto("files-folder-id", HosterFolderContentState.NotEmpty),
            new HosterFolderContentStateDto("parent-folder-id", HosterFolderContentState.NotEmpty),
        ]);
    }

    private void SetupFolderFileList(string folderId, int totalItems)
    {
        apiMock
            .Setup(x => x.ListFilesAsync("api-key", folderId, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new ListFilesResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Page = 1,
                        PerPage = 1,
                        TotalItems = totalItems,
                        Data = [],
                    }
                )
            );
    }

    private static ApiResponse<T> CreateApiResponse<T>(
        HttpStatusCode statusCode,
        T? content = default
    )
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(statusCode) { RequestMessage = new HttpRequestMessage() },
            content!,
            new RefitSettings(),
            error: null
        );
    }

    private static async Task<ApiResponse<T>> CreateErrorApiResponseAsync<T>(
        HttpStatusCode statusCode,
        string content
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://krakenfiles.test/api/file");
        var response = new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = new StringContent(content),
        };
        var error = await ApiException.Create(
            request,
            HttpMethod.Get,
            response,
            new RefitSettings()
        );

        return new ApiResponse<T>(response, default, new RefitSettings(), error);
    }
}
