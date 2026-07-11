using System.Net;
using Bearcat.Abstractions.Hoster.Dto;
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

    [Test]
    public async Task ChangeFileModeAsync_Mode_PassesNumericModeToApi()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };
        var expectedResponse = new UploadFileResponse
        {
            Status = (int)HttpStatusCode.OK,
            Response = new UploadFileResponse.ResponseObject
            {
                File = new UploadFileResponse.File
                {
                    FileId = "file-id",
                    Mode = 1,
                    ModeLabel = "Premium only",
                },
            },
        };

        SetupLogin();
        apiMock
            .Setup(x => x.ChangeFileModeAsync("token", "file-id", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await apiClient.ChangeFileModeAsync(
            config: config,
            fileId: "file-id",
            mode: UploadMode.PremiumOnly,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBeSameAs(expectedResponse);
    }

    [Test]
    public async Task UploadFileAsync_StreamLargerThanOneGiB_UsesLongContentLength()
    {
        // Arrange
        const long fileSize = 1024L * 1024 * 1024 + 1;
        var handler = new RecordingUploadHandler();
        using var httpClient = new HttpClient(handler);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(httpClient);
        var loggerMock = new Mock<ILogger<RapidgatorApiClient>>();
        var client = new RapidgatorApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            loggerMock.Object
        );
        await using var stream = new FileStream(
            Path.GetTempFileName(),
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 1,
            FileOptions.DeleteOnClose
        );
        stream.SetLength(fileSize);

        // Act
        var result = await client.UploadFileAsync(
            "https://upload.rapidgator.test",
            stream,
            "archive.part01.rar",
            CancellationToken.None
        );

        // Assert
        result.Status.ShouldBe((int)HttpStatusCode.OK);
        handler.ContentLength.ShouldNotBeNull();
        handler.ContentLength.Value.ShouldBeGreaterThan(fileSize);
    }

    [Test]
    public async Task CheckLinksAsync_OnlineFileInKnownFolder_MapsStatusAndDownloadCount()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };
        var fileUrl = "https://rapidgator.net/file/abc123";

        SetupLogin();

        apiMock
            .Setup(x =>
                x.CheckLinkAsync("token", It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    new CheckLinksResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Responses =
                        [
                            new CheckLinksResponse.ResponseObject
                            {
                                Url = fileUrl,
                                Filename = "archive.rar",
                                Status = "ACCESS",
                            },
                        ],
                    }
                )
            );

        apiMock
            .Setup(x =>
                x.GetFolderContentAsync("token", "folder-id", 1, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    new FolderContentResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Response = new FolderContentResponse.ResponseObject
                        {
                            Folder = new FolderContentResponse.FolderContent
                            {
                                Files =
                                [
                                    new FolderContentResponse.ContentFile
                                    {
                                        FileId = "abc123",
                                        Url = fileUrl,
                                        NbDownloads = 7,
                                    },
                                ],
                            },
                            Pager = new FolderContentResponse.Pager { Current = 1, Total = 1 },
                        },
                    }
                )
            );

        // Act
        var result = await apiClient.CheckLinksAsync(
            config,
            [new FileUrlToCheckDto(fileUrl, null, "folder-id")],
            CancellationToken.None
        );

        // Assert
        result[fileUrl].IsOnline.ShouldBeTrue();
        result[fileUrl].DownloadCount.ShouldBe(7);
    }

    [Test]
    public async Task CheckLinksAsync_OfflineFile_DoesNotFetchFolderContent()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };
        var fileUrl = "https://rapidgator.net/file/abc123";

        SetupLogin();

        apiMock
            .Setup(x =>
                x.CheckLinkAsync("token", It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    new CheckLinksResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Responses =
                        [
                            new CheckLinksResponse.ResponseObject
                            {
                                Url = fileUrl,
                                Filename = "archive.rar",
                                Status = "DELETED",
                            },
                        ],
                    }
                )
            );

        // Act
        var result = await apiClient.CheckLinksAsync(
            config,
            [new FileUrlToCheckDto(fileUrl, null, "folder-id")],
            CancellationToken.None
        );

        // Assert
        result[fileUrl].IsOnline.ShouldBeFalse();
        result[fileUrl].DownloadCount.ShouldBeNull();
        apiMock.Verify(
            x =>
                x.GetFolderContentAsync(
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
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
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(),
            },
            content,
            new RefitSettings(),
            error: null
        );
    }

    private sealed class RecordingUploadHandler : HttpMessageHandler
    {
        public long? ContentLength { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            ContentLength = request.Content?.Headers.ContentLength;

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"status\":200}"),
                }
            );
        }
    }
}
