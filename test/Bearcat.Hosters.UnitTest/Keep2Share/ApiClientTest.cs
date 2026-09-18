using System.Net;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Hosters.Keep2Share;
using Bearcat.Hosters.Keep2Share.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Keep2Share;

public class ApiClientTest
{
    private readonly List<string> temporaryFiles = [];

    private Mock<IKeep2ShareApi> apiMock = null!;
    private RecordingDownloadHandler downloadHandler = null!;
    private ApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IKeep2ShareApi>(MockBehavior.Strict);
        downloadHandler = new RecordingDownloadHandler();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<ILogger<ApiClient>>();

        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.DownloadHttpClientName))
            .Returns(() => new HttpClient(downloadHandler, disposeHandler: false));

        apiClient = new ApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            new HosterFileDownloader(new HttpClientProvider(httpClientFactoryMock.Object)),
            loggerMock.Object
        )
        {
            RateLimitRetryDelay = TimeSpan.Zero,
        };
    }

    [TearDown]
    public void TearDown()
    {
        downloadHandler.Dispose();

        foreach (var temporaryFile in temporaryFiles.Where(File.Exists))
        {
            File.Delete(temporaryFile);
        }
    }

    [Test]
    public async Task RequestUploadAsync_ParentId_PassesParentIdToApi()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };

        SetupLogin();

        apiMock
            .Setup(x =>
                x.GetUploadFormDataAsync(
                    It.Is<UploadFormDataRequest>(request =>
                        request.AuthToken == "auth-token" && request.ParentId == "folder-id"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFormDataResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    FormAction = "https://upload.keep2share.test",
                    FileField = "file",
                }
            );

        // Act
        var result = await apiClient.RequestUploadAsync(
            config,
            "folder-id",
            CancellationToken.None
        );

        // Assert
        result.FormAction.ShouldBe("https://upload.keep2share.test");
    }

    [Test]
    public async Task CreateFolderAsync_ExistingRootFolderWithName_ReturnsExistingFolderId()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };

        SetupLogin();

        apiMock
            .Setup(x =>
                x.GetFoldersListAsync(
                    It.Is<FolderListRequest>(request =>
                        request.AuthToken == "auth-token" && request.ParentId == null
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FolderListResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    FoldersList = ["/other-folder", "/release-folder"],
                    FoldersIds = ["other-folder-id", "existing-folder-id"],
                }
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            config,
            "release-folder",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("existing-folder-id");
        apiMock.Verify(
            x =>
                x.CreateFolderAsync(It.IsAny<CreateFolderRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Test]
    public async Task CreateFolderAsync_NoExistingRootFolderWithName_CreatesFolder()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };

        SetupLogin();

        apiMock
            .Setup(x =>
                x.GetFoldersListAsync(
                    It.Is<FolderListRequest>(request =>
                        request.AuthToken == "auth-token" && request.ParentId == null
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FolderListResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    FoldersList = ["/other-folder"],
                    FoldersIds = ["other-folder-id"],
                }
            );

        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    It.Is<CreateFolderRequest>(request =>
                        request.AuthToken == "auth-token"
                        && request.Name == "release-folder"
                        && request.Parent == "/"
                        && request.Access == "public"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new CreateFolderResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    Id = "created-folder-id",
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
    public async Task CheckLinksAsync_ApiReturnsFileInfos_MapsStatusesToOriginalUrls()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };
        var fileUrls = new[]
        {
            "https://k2s.cc/file/online-id",
            "https://k2s.cc/file/offline-id",
            "https://k2s.cc/file/missing-id",
            "not-a-url",
        };

        SetupLogin();

        apiMock
            .Setup(x =>
                x.GetFilesInfoAsync(
                    It.Is<GetFilesInfoRequest>(request =>
                        request.AuthToken == "auth-token"
                        && request.Ids.Count == 3
                        && request.Ids.Contains("online-id")
                        && request.Ids.Contains("offline-id")
                        && request.Ids.Contains("missing-id")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new GetFilesInfoResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    Files =
                    [
                        new GetFilesInfoResponse.FileInfo { Id = "online-id", IsAvailable = true },
                        new GetFilesInfoResponse.FileInfo
                        {
                            Id = "offline-id",
                            IsAvailable = false,
                        },
                        new GetFilesInfoResponse.FileInfo { Id = "missing-id", IsAvailable = null },
                    ],
                }
            );

        // Act
        var result = await apiClient.CheckLinksAsync(config, fileUrls, CancellationToken.None);

        // Assert
        result[fileUrls[0]].ShouldBeTrue();
        result[fileUrls[1]].ShouldBeFalse();
        result[fileUrls[2]].ShouldBeFalse();
        result[fileUrls[3]].ShouldBeFalse();
        apiMock.Verify(
            x => x.GetFileStatusAsync(It.IsAny<FileStatusRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Test]
    public async Task CheckLinksAsync_TooManyRequests_RetriesBatch()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };
        var fileUrl = "https://k2s.cc/file/file-1";
        var calls = 0;

        SetupLogin();

        apiMock
            .Setup(x =>
                x.GetFilesInfoAsync(It.IsAny<GetFilesInfoRequest>(), It.IsAny<CancellationToken>())
            )
            .Returns(() =>
            {
                calls++;

                if (calls == 1)
                {
                    throw new HttpRequestException(
                        "rate limited",
                        inner: null,
                        statusCode: HttpStatusCode.TooManyRequests
                    );
                }

                return Task.FromResult(
                    new GetFilesInfoResponse
                    {
                        Status = "success",
                        Code = (int)HttpStatusCode.OK,
                        Files =
                        [
                            new GetFilesInfoResponse.FileInfo { Id = "file-1", IsAvailable = true },
                        ],
                    }
                );
            });

        // Act
        var result = await apiClient.CheckLinksAsync(config, [fileUrl], CancellationToken.None);

        // Assert
        result[fileUrl].ShouldBeTrue();
        calls.ShouldBe(2);
    }

    [Test]
    public async Task RequestUploadAsync_UploadFormReturnsCaptchaError_ThrowsCaptchaRequired()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };

        apiMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new LoginResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    AuthToken = "auth-token",
                }
            );

        apiMock
            .Setup(x =>
                x.GetUploadFormDataAsync(
                    It.IsAny<UploadFormDataRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFormDataResponse
                {
                    Status = "error",
                    Code = (int)HttpStatusCode.BadRequest,
                    ErrorCode = 2,
                    Message = "Invalid request params",
                }
            );

        // Act + Assert
        var exception = await Should.ThrowAsync<CaptchaVerificationRequiredException>(() =>
            apiClient.RequestUploadAsync(config, null, CancellationToken.None)
        );
        exception.Code.ShouldBe((int)HttpStatusCode.BadRequest);
        exception.ErrorCode.ShouldBe(2);
    }

    [Test]
    public async Task DownloadFileAsync_ApiReturnsUrl_WritesFileToTargetPath()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };
        var targetFilePath = CreateTemporaryFilePath();

        SetupLogin();

        apiMock
            .Setup(x =>
                x.GetUrlAsync(
                    It.Is<GetUrlRequest>(request =>
                        request.AuthToken == "auth-token" && request.FileId == "abc123def456"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new GetUrlResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    Url = "https://dl.k2s.test/download/abc123def456",
                }
            );

        downloadHandler.RespondWith("archive-content");

        // Act
        await apiClient.DownloadFileAsync(
            config: config,
            fileUrl: "https://keep2share.cc/file/abc123def456",
            targetFilePath: targetFilePath,
            progress: NullDownloadProgress.Instance,
            expectedSizeBytes: 15,
            cancellationToken: CancellationToken.None
        );

        // Assert
        (await File.ReadAllTextAsync(targetFilePath)).ShouldBe("archive-content");
        downloadHandler
            .RequestedUrls.ShouldHaveSingleItem()
            .ShouldBe("https://dl.k2s.test/download/abc123def456");
    }

    [Test]
    public async Task DownloadFileAsync_ApiReturnsNoUrl_ThrowsWithMessageAndSkipsDownload()
    {
        // Arrange
        const string message = "Please verify your request via re captcha challenge";
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };
        var targetFilePath = CreateTemporaryFilePath();

        SetupLogin();

        apiMock
            .Setup(x => x.GetUrlAsync(It.IsAny<GetUrlRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new GetUrlResponse
                {
                    Status = "error",
                    Code = (int)HttpStatusCode.NotAcceptable,
                    ErrorCode = 33,
                    Message = message,
                }
            );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.DownloadFileAsync(
                config: config,
                fileUrl: "https://keep2share.cc/file/abc123def456",
                targetFilePath: targetFilePath,
                progress: NullDownloadProgress.Instance,
                expectedSizeBytes: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe(message);
        downloadHandler.RequestedUrls.ShouldBeEmpty();
        File.Exists(targetFilePath).ShouldBeFalse();
    }

    [Test]
    public async Task DownloadFileAsync_ApiThrowsErrorResponse_ThrowsWithApiMessageAndSkipsDownload()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };
        var targetFilePath = CreateTemporaryFilePath();

        SetupLogin();

        apiMock
            .Setup(x => x.GetUrlAsync(It.IsAny<GetUrlRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                await CreateApiExceptionAsync(
                    HttpStatusCode.NotAcceptable,
                    """{"status":"error","code":406,"message":"File not found","errorCode":20}"""
                )
            );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.DownloadFileAsync(
                config: config,
                fileUrl: "https://keep2share.cc/file/abc123def456",
                targetFilePath: targetFilePath,
                progress: NullDownloadProgress.Instance,
                expectedSizeBytes: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe("File not found");
        downloadHandler.RequestedUrls.ShouldBeEmpty();
        File.Exists(targetFilePath).ShouldBeFalse();
    }

    [Test]
    public async Task DownloadFileAsync_ApiThrowsCaptchaResponse_ThrowsCaptchaRequired()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };
        var targetFilePath = CreateTemporaryFilePath();

        SetupLogin();

        apiMock
            .Setup(x => x.GetUrlAsync(It.IsAny<GetUrlRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                await CreateApiExceptionAsync(
                    HttpStatusCode.NotAcceptable,
                    """{"status":"error","code":406,"message":"Please verify your request via re captcha challenge","errorCode":33}"""
                )
            );

        // Act
        var exception = await Should.ThrowAsync<CaptchaVerificationRequiredException>(() =>
            apiClient.DownloadFileAsync(
                config: config,
                fileUrl: "https://keep2share.cc/file/abc123def456",
                targetFilePath: targetFilePath,
                progress: NullDownloadProgress.Instance,
                expectedSizeBytes: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe("Please verify your request via re captcha challenge");
        exception.ErrorCode.ShouldBe(33);
        downloadHandler.RequestedUrls.ShouldBeEmpty();
    }

    [Test]
    public async Task DownloadFileAsync_ApiThrowsUnparseableBody_ThrowsWithExceptionMessage()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };
        var targetFilePath = CreateTemporaryFilePath();

        SetupLogin();

        apiMock
            .Setup(x => x.GetUrlAsync(It.IsAny<GetUrlRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                await CreateApiExceptionAsync(
                    HttpStatusCode.InternalServerError,
                    "<html>Internal Server Error</html>"
                )
            );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.DownloadFileAsync(
                config: config,
                fileUrl: "https://keep2share.cc/file/abc123def456",
                targetFilePath: targetFilePath,
                progress: NullDownloadProgress.Instance,
                expectedSizeBytes: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.ShouldBeOfType<HttpRequestException>();
        exception.Message.ShouldNotBeNullOrWhiteSpace();
        downloadHandler.RequestedUrls.ShouldBeEmpty();
        File.Exists(targetFilePath).ShouldBeFalse();
    }

    [Test]
    public async Task GetFileSizesAsync_ApiReturnsSizes_MapsThemBackToFileUrls()
    {
        // Arrange
        var config = new Keep2ShareConfig
        {
            EmailAddress = "user@example.test",
            Password = "password",
        };

        SetupLogin();

        apiMock
            .Setup(x =>
                x.GetFilesInfoAsync(
                    It.Is<GetFilesInfoRequest>(request =>
                        request.AuthToken == "auth-token" && request.Ids.Count == 2
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new GetFilesInfoResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    Files =
                    [
                        new GetFilesInfoResponse.FileInfo
                        {
                            Id = "abc123def456",
                            IsAvailable = true,
                            Size = 106954766,
                        },
                        new GetFilesInfoResponse.FileInfo
                        {
                            Id = "ghi789jkl012",
                            IsAvailable = false,
                        },
                    ],
                }
            );

        // Act
        var result = await apiClient.GetFileSizesAsync(
            config,
            ["https://keep2share.cc/file/abc123def456", "https://keep2share.cc/file/ghi789jkl012"],
            CancellationToken.None
        );

        // Assert
        result["https://keep2share.cc/file/abc123def456"].ShouldBe(106954766);
        result.ShouldNotContainKey("https://keep2share.cc/file/ghi789jkl012");
    }

    private static async Task<ApiException> CreateApiExceptionAsync(
        HttpStatusCode statusCode,
        string content
    )
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://keep2share.cc/api/v2/geturl"
        );
        var response = new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = new StringContent(content),
        };

        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }

    private string CreateTemporaryFilePath()
    {
        var targetFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.rar");
        temporaryFiles.Add(targetFilePath);
        temporaryFiles.Add(targetFilePath + ".part");

        return targetFilePath;
    }

    private sealed class RecordingDownloadHandler : HttpMessageHandler
    {
        private string content = string.Empty;

        public List<string> RequestedUrls { get; } = [];

        public void RespondWith(string responseContent)
        {
            content = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestedUrls.Add(request.RequestUri!.ToString());

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content),
                    RequestMessage = request,
                }
            );
        }
    }

    private void SetupLogin()
    {
        apiMock
            .Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new LoginResponse
                {
                    Status = "success",
                    Code = (int)HttpStatusCode.OK,
                    AuthToken = "auth-token",
                }
            );
    }
}
