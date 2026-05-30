using System.Net;
using System.Text.Json;
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
    public void CreateFolderRequest_SerializesNameAsLowercase()
    {
        // Arrange
        var request = new CreateFolderRequest("release-folder");

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        json.ShouldBe("""{"name":"release-folder"}""");
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
    public async Task UploadFileAsync_FolderId_AddsFolderIdToMultipartForm()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };
        var httpMessageHandler = new TestHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "status": 200,
                      "data": {
                        "url": "https://krakenfiles.com/view/hash/file.bin"
                      }
                    }
                    """
                ),
            }
        );
        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.UploadHttpClientName))
            .Returns(new HttpClient(httpMessageHandler));
        apiMock
            .Setup(x => x.GetAvailableServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new AvailableServerResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Data = new AvailableServerData
                    {
                        Url = "https://uploads.krakenfiles.test/uploader/api/file",
                        ServerAccessToken = "server-token",
                    },
                }
            );

        // Act
        await using var stream = new MemoryStream([1, 2, 3]);
        await apiClient.UploadFileAsync(
            config,
            stream,
            "file.bin",
            "folder-id",
            CancellationToken.None
        );

        // Assert
        httpMessageHandler.RequestContent.ShouldNotBeNull();
        var multipartContent = httpMessageHandler.RequestContent;
        multipartContent.ShouldContain("name=folderId");
        multipartContent.ShouldContain("folder-id");
    }

    [Test]
    public async Task IsApiKeyValidAsync_FileNotFoundForProbeHash_ReturnsTrue()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.GetFileAsync(
                    "bearcat-login-check",
                    "api-key",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateApiResponse<FileResponse>(HttpStatusCode.NotFound));

        // Act
        var result = await apiClient.IsApiKeyValidAsync(config, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
    }

    [Test]
    public async Task IsApiKeyValidAsync_UnauthorizedForProbeHash_ReturnsFalse()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.GetFileAsync(
                    "bearcat-login-check",
                    "api-key",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateApiResponse<FileResponse>(HttpStatusCode.Unauthorized));

        // Act
        var result = await apiClient.IsApiKeyValidAsync(config, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    private static ApiResponse<T> CreateApiResponse<T>(HttpStatusCode statusCode, T? content = default)
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(statusCode),
            content!,
            new RefitSettings(),
            error: null
        );
    }

    private sealed class TestHttpMessageHandler(HttpResponseMessage response)
        : HttpMessageHandler
    {
        public string? RequestContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestContent = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return response;
        }
    }
}
