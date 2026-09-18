using System.Net;
using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.Alfafile;
using Bearcat.Hosters.Alfafile.Api;
using Bearcat.Hosters.Alfafile.Api.File;
using Bearcat.Hosters.Alfafile.Api.Folder;
using Bearcat.Hosters.Alfafile.Api.User;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Alfafile;

public class ApiClientTest
{
    private readonly AlfafileConfig config = new()
    {
        Username = "user@example.test",
        Password = "password",
    };

    private readonly List<string> temporaryFiles = [];

    private Mock<IAlfafileApi> apiMock = null!;
    private RecordingDownloadHandler downloadHandler = null!;
    private ApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IAlfafileApi>(MockBehavior.Strict);
        downloadHandler = new RecordingDownloadHandler();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<ILogger<ApiClient>>();

        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.DownloadHttpClientName))
            .Returns(() => new HttpClient(downloadHandler, disposeHandler: false));

        apiMock
            .Setup(x =>
                x.LoginAsync(config.Username, config.Password, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    new LoginResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Response = new LoginResponse.ResponseObject { Token = "auth-token" },
                    }
                )
            );

        apiMock
            .Setup(x =>
                x.GetFolderContentAsync(
                    "auth-token",
                    It.IsAny<string?>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateApiResponse(
                    new FolderContentResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Response = new FolderContentResponse.ResponseObject
                        {
                            Pager = new FolderContentResponse.Pager { Current = 1, Total = 1 },
                        },
                    }
                )
            );

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
    public async Task DownloadFileAsync_ApiReturnsDownloadUrl_WritesFileToTargetPath()
    {
        // Arrange
        var targetFilePath = CreateTemporaryFilePath();

        apiMock
            .Setup(x => x.DownloadFileAsync("auth-token", "ACsST", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new DownloadFileResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Response = new DownloadFileResponse.ResponseObject
                    {
                        DownloadUrl = "https://s114.alfafile.net/download/72b2831b",
                    },
                }
            );

        downloadHandler.RespondWith("archive-content");

        // Act
        await apiClient.DownloadFileAsync(
            config: config,
            fileUrl: "https://alfafile.net/file/ACsST",
            targetFilePath: targetFilePath,
            progress: NullDownloadProgress.Instance,
            expectedSizeBytes: 15,
            cancellationToken: CancellationToken.None
        );

        // Assert
        (await File.ReadAllTextAsync(targetFilePath)).ShouldBe("archive-content");
        downloadHandler
            .RequestedUrls.ShouldHaveSingleItem()
            .ShouldBe("https://s114.alfafile.net/download/72b2831b");
    }

    [Test]
    public async Task DownloadFileAsync_ApiReportsDownloadDelay_ThrowsWithDetailsAndSkipsDownload()
    {
        // Arrange
        const string details =
            "Conflict. Delay between downloads must be not less than 120 minutes. Try again in 119 minutes.";
        var targetFilePath = CreateTemporaryFilePath();

        apiMock
            .Setup(x => x.DownloadFileAsync("auth-token", "ACsST", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new DownloadFileResponse
                {
                    Status = (int)HttpStatusCode.Conflict,
                    Response = null,
                    Details = details,
                }
            );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.DownloadFileAsync(
                config: config,
                fileUrl: "https://alfafile.net/file/ACsST",
                targetFilePath: targetFilePath,
                progress: NullDownloadProgress.Instance,
                expectedSizeBytes: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe(details);
        downloadHandler.RequestedUrls.ShouldBeEmpty();
        File.Exists(targetFilePath).ShouldBeFalse();
    }

    [Test]
    public async Task RequestUploadFileAsync_FolderId_PassesFolderIdToApi()
    {
        // Arrange
        apiMock
            .Setup(x =>
                x.RequestUploadFileAsync(
                    "auth-token",
                    "archive.part01.rar",
                    1024,
                    "hash",
                    "folder-id",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Response = new UploadFileResponse.ResponseObject
                    {
                        Upload = new UploadFileResponse.Upload { UploadId = "upload-id" },
                    },
                }
            );

        // Act
        var result = await apiClient.RequestUploadFileAsync(
            "archive.part01.rar",
            1024,
            "hash",
            "folder-id",
            config,
            CancellationToken.None
        );

        // Assert
        result.Status.ShouldBe((int)HttpStatusCode.OK);
    }

    [Test]
    public async Task CreateFolderAsync_CreatedFolder_ReturnsFolderId()
    {
        // Arrange
        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    "auth-token",
                    "release-folder",
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FolderResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Response = new FolderResponse.ResponseObject
                    {
                        Folder = new FolderResponse.Folder
                        {
                            FolderId = "created-folder-id",
                            Name = "release-folder",
                        },
                    },
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
        apiMock.Verify(
            x =>
                x.GetFolderInfoAsync(
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task CreateFolderAsync_FolderNameConflict_ReturnsExistingRootFolderId()
    {
        // Arrange
        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    "auth-token",
                    "release-folder",
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FolderResponse
                {
                    Status = (int)HttpStatusCode.Conflict,
                    Details = "Conflict. Folder with the same name already exists",
                }
            );

        apiMock
            .Setup(x => x.GetFolderInfoAsync("auth-token", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new FolderResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Response = new FolderResponse.ResponseObject
                    {
                        Folder = new FolderResponse.Folder
                        {
                            Folders =
                            [
                                new FolderResponse.Folder
                                {
                                    FolderId = "other-folder-id",
                                    Name = "other-folder",
                                },
                                new FolderResponse.Folder
                                {
                                    FolderId = "existing-folder-id",
                                    Name = "release-folder",
                                },
                            ],
                        },
                    },
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
    }

    [Test]
    public async Task CheckLinksAsync_ManyLinks_ChecksUpToTenLinksInParallel()
    {
        // Arrange
        var fileUrls = Enumerable
            .Range(1, 25)
            .Select(index => $"https://alfafile.net/file/file-{index}")
            .ToList();
        var currentParallelRequests = 0;
        var maximumParallelRequests = 0;

        apiMock
            .Setup(x =>
                x.GetFileInfoAsync("auth-token", It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .Returns(async () =>
            {
                var current = Interlocked.Increment(ref currentParallelRequests);
                UpdateMaximum(ref maximumParallelRequests, current);

                await Task.Delay(25);

                Interlocked.Decrement(ref currentParallelRequests);

                return CreateApiResponse(
                    new FileInfoResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Response = new FileInfoResponse.ResponseObject
                        {
                            File = new UploadedFile(),
                        },
                    }
                );
            });

        // Act
        var result = await apiClient.CheckLinksAsync(config, fileUrls, CancellationToken.None);

        // Assert
        result.Count.ShouldBe(25);
        result.Values.ShouldAllBe(status => status.IsOnline);
        maximumParallelRequests.ShouldBe(10);
    }

    [Test]
    public async Task CheckLinksAsync_TooManyRequests_RetriesLink()
    {
        // Arrange
        var fileUrl = "https://alfafile.net/file/file-1";
        var calls = 0;

        apiMock
            .Setup(x => x.GetFileInfoAsync("auth-token", "file-1", It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls++;

                if (calls == 1)
                {
                    return Task.FromResult(
                        CreateApiResponse(new FileInfoResponse(), HttpStatusCode.TooManyRequests)
                    );
                }

                return Task.FromResult(
                    CreateApiResponse(
                        new FileInfoResponse
                        {
                            Status = (int)HttpStatusCode.OK,
                            Response = new FileInfoResponse.ResponseObject
                            {
                                File = new UploadedFile(),
                            },
                        }
                    )
                );
            });

        // Act
        var result = await apiClient.CheckLinksAsync(config, [fileUrl], CancellationToken.None);

        // Assert
        result[fileUrl].IsOnline.ShouldBeTrue();
        calls.ShouldBe(2);
    }

    [Test]
    public async Task CheckLinksAsync_PersistentTooManyRequests_OmitsLinkInsteadOfMarkingOffline()
    {
        // Arrange
        var fileUrl = "https://alfafile.net/file/file-1";

        apiMock
            .Setup(x => x.GetFileInfoAsync("auth-token", "file-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(new FileInfoResponse(), HttpStatusCode.TooManyRequests)
            );

        // Act
        var result = await apiClient.CheckLinksAsync(config, [fileUrl], CancellationToken.None);

        // Assert
        result.ShouldNotContainKey(fileUrl);
    }

    [Test]
    public async Task CheckLinksAsync_FolderContentReportsDownloads_MapsDownloadCountToFileUrl()
    {
        // Arrange
        var fileUrl = "https://alfafile.net/file/GA";

        apiMock
            .Setup(x => x.GetFileInfoAsync("auth-token", "GA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    new FileInfoResponse
                    {
                        Status = (int)HttpStatusCode.OK,
                        Response = new FileInfoResponse.ResponseObject
                        {
                            File = new UploadedFile { FileId = "GA", FolderId = "7" },
                        },
                    }
                )
            );

        apiMock
            .Setup(x =>
                x.GetFolderContentAsync("auth-token", "7", 1, It.IsAny<CancellationToken>())
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
                                        FileId = "GA",
                                        NbDownloads = 42,
                                    },
                                ],
                            },
                            Pager = new FolderContentResponse.Pager { Current = 1, Total = 1 },
                        },
                    }
                )
            );

        // Act
        var result = await apiClient.CheckLinksAsync(config, [fileUrl], CancellationToken.None);

        // Assert
        result[fileUrl].IsOnline.ShouldBeTrue();
        result[fileUrl].DownloadCount.ShouldBe(42);
    }

    private static ApiResponse<T> CreateApiResponse<T>(
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

    [Test]
    public async Task MoveFileToFolderAsync_ApiReportsSuccess_MovesFileByExtractedFileId()
    {
        // Arrange
        apiMock
            .Setup(x =>
                x.MoveFileAsync("auth-token", "Gu", "dest-folder", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new MoveFileResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Response = new MoveFileResponse.ResponseObject
                    {
                        Result = new MoveFileResponse.ResultObject
                        {
                            Success = 1,
                            SuccessIds = ["Gu"],
                        },
                    },
                }
            );

        // Act
        await apiClient.MoveFileToFolderAsync(
            config,
            "https://alfafile.net/file/Gu",
            "dest-folder",
            CancellationToken.None
        );

        // Assert
        apiMock.Verify(
            x => x.MoveFileAsync("auth-token", "Gu", "dest-folder", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task MoveFileToFolderAsync_ApiReportsFailure_Throws()
    {
        // Arrange
        apiMock
            .Setup(x =>
                x.MoveFileAsync("auth-token", "Gu", "dest-folder", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new MoveFileResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Response = new MoveFileResponse.ResponseObject
                    {
                        Result = new MoveFileResponse.ResultObject
                        {
                            Success = 0,
                            Fail = 1,
                            FailIds = ["Gu"],
                            Errors = ["Conflict. File with the same name already exists"],
                        },
                    },
                }
            );

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.MoveFileToFolderAsync(
                config,
                "https://alfafile.net/file/Gu",
                "dest-folder",
                CancellationToken.None
            )
        );
    }

    private string CreateTemporaryFilePath()
    {
        var filePath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid()}.rar");
        temporaryFiles.Add(filePath);
        temporaryFiles.Add(filePath + ".part");

        return filePath;
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

    private static void UpdateMaximum(ref int maximum, int current)
    {
        var initialMaximum = maximum;

        while (
            current > initialMaximum
            && Interlocked.CompareExchange(ref maximum, current, initialMaximum) != initialMaximum
        )
        {
            initialMaximum = maximum;
        }
    }
}
