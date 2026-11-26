using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Fichier;
using Bearcat.Hosters.Fichier.Api;
using Bearcat.Hosters.Fichier.Api.Download;
using Bearcat.Hosters.Fichier.Api.Folder;
using Bearcat.Hosters.Fichier.Api.User;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Fichier;

public class ApiClientTest
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
    };

    private Mock<IFichierApi> apiMock = null!;
    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private TestHttpMessageHandler httpMessageHandler = null!;
    private ApiClient apiClient = null!;

    [SetUp]
    public void SetUp()
    {
        apiMock = new Mock<IFichierApi>(MockBehavior.Strict);
        httpClientFactoryMock = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpMessageHandler = new TestHttpMessageHandler();
        var loggerMock = new Mock<ILogger<ApiClient>>();

        httpClientFactoryMock
            .Setup(x => x.CreateClient(ApiClient.UploadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        httpClientFactoryMock
            .Setup(x => x.CreateClient(ApiClient.CheckLinksHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        httpClientFactoryMock
            .Setup(x => x.CreateClient(HttpClientProvider.DownloadHttpClientName))
            .Returns(() => new HttpClient(httpMessageHandler, disposeHandler: false));

        apiClient = new ApiClient(
            apiMock.Object,
            new HttpClientProvider(httpClientFactoryMock.Object),
            new HosterFileDownloader(new HttpClientProvider(httpClientFactoryMock.Object)),
            loggerMock.Object
        )
        {
            RateLimitRetryDelay = TimeSpan.Zero,
            FolderListRequestDelay = TimeSpan.Zero,
        };
    }

    [TearDown]
    public void TearDown()
    {
        httpMessageHandler.Dispose();
    }

    [Test]
    public async Task UploadFileAsync_SendsRcloneCompatibleMultipartAndReturnsDownloadLink()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        var capturedUploadBody = string.Empty;
        var capturedUploadContentType = string.Empty;

        httpMessageHandler.Enqueue(async request =>
        {
            request.Method.ShouldBe(HttpMethod.Get);
            request
                .RequestUri!.ToString()
                .ShouldBe("https://api.1fichier.com/v1/upload/get_upload_server.cgi");
            request.Headers.Authorization.ShouldNotBeNull();
            request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
            request.Headers.Authorization.Parameter.ShouldBe("api-key");
            request.Content.ShouldNotBeNull();
            request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");

            return CreateJsonResponse("""{"id":"Upload1234","url":"up1.1fichier.test"}""");
        });

        httpMessageHandler.Enqueue(async request =>
        {
            request.Method.ShouldBe(HttpMethod.Post);
            request
                .RequestUri!.ToString()
                .ShouldBe("https://up1.1fichier.test/upload.cgi?id=Upload1234");
            request.Headers.Authorization.ShouldNotBeNull();
            request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
            request.Headers.Authorization.Parameter.ShouldBe("api-key");

            capturedUploadContentType = request.Content!.Headers.ContentType!.ToString();
            capturedUploadBody = await request.Content.ReadAsStringAsync();

            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("/end.pl?xid=Upload1234", UriKind.Relative);
            return response;
        });

        httpMessageHandler.Enqueue(request =>
        {
            request.Method.ShouldBe(HttpMethod.Get);
            request
                .RequestUri!.ToString()
                .ShouldBe("https://up1.1fichier.test/end.pl?xid=Upload1234");
            request.Headers.TryGetValues("JSON", out var jsonHeaderValues).ShouldBeTrue();
            jsonHeaderValues!.Single().ShouldBe("1");

            return Task.FromResult(
                CreateJsonResponse(
                    """
                    {
                      "incoming": 0,
                      "links": [
                        {
                          "download": "https://1fichier.com/?abc",
                          "filename": "archive.part01.rar",
                          "size": "14"
                        }
                      ]
                    }
                    """
                )
            );
        });

        await using var stream = new MemoryStream("upload-content"u8.ToArray());

        // Act
        var result = await apiClient.UploadFileAsync(
            config,
            stream,
            "archive.part01.rar",
            null,
            CancellationToken.None
        );

        // Assert
        result.Links.Single().Download.ShouldBe("https://1fichier.com/?abc");
        capturedUploadContentType.ShouldStartWith("multipart/form-data; boundary=");
        capturedUploadContentType.ShouldNotContain("boundary=\"");
        capturedUploadBody.ShouldContain("Content-Disposition: form-data; name=\"did\"\r\n\r\n0");
        capturedUploadBody.ShouldContain(
            "Content-Disposition: form-data; name=\"file[]\"; filename=\"archive.part01.rar\"\r\n\r\nupload-content"
        );
        capturedUploadBody.ShouldNotContain("filename*=");
        capturedUploadBody.ShouldNotContain("Content-Type: application/octet-stream");
        httpMessageHandler.PendingRequests.ShouldBe(0);
    }

    [Test]
    public async Task CreateFolderAsync_ExistingRootFolderWithName_ReturnsExistingFolderId()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        apiMock
            .Setup(x =>
                x.GetFolderListAsync(
                    "Bearer api-key",
                    It.Is<FolderListRequest>(request => request.FolderId == 0),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FolderListResponse
                {
                    Status = "OK",
                    FolderId = 0,
                    SubFolders =
                    [
                        new FolderListResponse.Folder { Id = 222, Name = "other-folder" },
                        new FolderListResponse.Folder { Id = 123, Name = "release-folder" },
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
        result.ShouldBe("123");
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
    public async Task CreateFolderAsync_NoExistingRootFolderWithName_CreatesFolder()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        apiMock
            .Setup(x =>
                x.GetFolderListAsync(
                    "Bearer api-key",
                    It.Is<FolderListRequest>(request => request.FolderId == 0),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FolderListResponse
                {
                    Status = "OK",
                    FolderId = 0,
                    SubFolders =
                    [
                        new FolderListResponse.Folder { Id = 222, Name = "other-folder" },
                    ],
                }
            );
        apiMock
            .Setup(x =>
                x.CreateFolderAsync(
                    "Bearer api-key",
                    It.Is<CreateFolderRequest>(request =>
                        request.Name == "release-folder" && request.FolderId == 0
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new CreateFolderResponse
                {
                    Status = "OK",
                    FolderId = 123,
                    Name = "release-folder",
                }
            );

        // Act
        var result = await apiClient.CreateFolderAsync(
            config,
            "release-folder",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("123");
    }

    [Test]
    public async Task CheckLinksAsync_CheckLinksResponse_MapsOnlineAndPrivateLinksOnlineAndDeletedAndInvalidLinksOffline()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        string[] fileUrls =
        [
            "https://1fichier.com/?online",
            "https://1fichier.com/?deleted",
            "https://1fichier.com/?invalid",
            "https://1fichier.com/?private",
        ];
        var capturedRequestBody = string.Empty;

        httpMessageHandler.Enqueue(async request =>
        {
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri!.ToString().ShouldBe("https://1fichier.com/check_links.pl");
            capturedRequestBody = await request.Content!.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    https://1fichier.com/?private;;;PRIVATE
                    https://1fichier.com/?deleted;;;NOT FOUND
                    https://1fichier.com/?invalid;;;BAD LINK
                    https://1fichier.com/?online;archive.part1.rar;1050673174

                    """
                ),
            };
        });

        // Act
        var result = await apiClient.CheckLinksAsync(config, fileUrls, CancellationToken.None);

        // Assert
        capturedRequestBody.ShouldBe(
            string.Join(
                '&',
                fileUrls.Select(fileUrl => $"links%5B%5D={Uri.EscapeDataString(fileUrl)}")
            )
        );
        result[fileUrls[0]].ShouldBeTrue();
        result[fileUrls[1]].ShouldBeFalse();
        result[fileUrls[2]].ShouldBeFalse();
        result[fileUrls[3]].ShouldBeTrue();
    }

    [TestCase(HttpStatusCode.Forbidden, "<html><body>Access denied</body></html>")]
    [TestCase(HttpStatusCode.OK, "<html><body>Flood detected</body></html>")]
    [TestCase(HttpStatusCode.OK, "https://1fichier.com/?abc;;;LOCKED")]
    [TestCase(HttpStatusCode.OK, "https://1fichier.com/?other;archive.part1.rar;1050673174")]
    [TestCase(HttpStatusCode.OK, "")]
    public async Task CheckLinksAsync_ResponseWithoutKnownStatusForLink_ThrowsInsteadOfMarkingOffline(
        HttpStatusCode statusCode,
        string content
    )
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        httpMessageHandler.Enqueue(_ =>
            Task.FromResult(
                new HttpResponseMessage(statusCode) { Content = new StringContent(content) }
            )
        );

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.CheckLinksAsync(config, ["https://1fichier.com/?abc"], CancellationToken.None)
        );
    }

    [Test]
    public async Task CheckLinksAsync_MoreLinksThanOneRequestAllows_SendsOneRequestPerHundredLinks()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        var fileUrls = Enumerable
            .Range(1, 150)
            .Select(index => $"https://1fichier.com/?file{index}")
            .ToList();

        foreach (var fileUrlBatch in fileUrls.Chunk(100))
        {
            httpMessageHandler.Enqueue(_ =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            string.Join(
                                '\n',
                                fileUrlBatch.Select(fileUrl => $"{fileUrl};;;NOT FOUND")
                            )
                        ),
                    }
                )
            );
        }

        // Act
        var result = await apiClient.CheckLinksAsync(config, fileUrls, CancellationToken.None);

        // Assert
        result.Count.ShouldBe(150);
        httpMessageHandler.PendingRequests.ShouldBe(0);
    }

    [Test]
    public async Task GetUserInfoAsync_NotAuthenticated_ThrowsHosterCredentialsRejected()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "" };

        apiMock
            .Setup(x =>
                x.GetUserInfoAsync(
                    It.IsAny<string>(),
                    It.IsAny<UserInfoRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                await CreateErrorApiResponseAsync<UserInfoResponse>(
                    HttpStatusCode.Unauthorized,
                    """{"message":"Not authenticated #247","status":"KO"}"""
                )
            );

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            apiClient.GetUserInfoAsync(config, CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldBe("Not authenticated #247");
    }

    [Test]
    public async Task GetFileSizesAsync_CheckLinksResponse_ReturnsSizesOfOnlineLinks()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        var onlineFileUrl = "https://1fichier.com/?abc";
        var deletedFileUrl = "https://1fichier.com/?def";

        httpMessageHandler.Enqueue(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $"{deletedFileUrl};;;NOT FOUND\n{onlineFileUrl};archive.part01.rar;104857600\n"
                    ),
                }
            )
        );

        // Act
        var result = await apiClient.GetFileSizesAsync(
            config,
            [onlineFileUrl, deletedFileUrl],
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(new Dictionary<string, long> { [onlineFileUrl] = 104857600 });
    }

    [Test]
    public async Task DownloadFileAsync_TokenEndpointReturnsUrl_WritesFileToTargetPath()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.GetDownloadTokenAsync(
                    "Bearer api-key",
                    It.Is<DownloadTokenRequest>(r => r.Url == "https://1fichier.com/?abc123"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new DownloadTokenResponse
                    {
                        Status = "OK",
                        Url = "https://node.1fichier.test/token",
                    }
                )
            );

        httpMessageHandler.Enqueue(request =>
        {
            request.RequestUri!.ToString().ShouldBe("https://node.1fichier.test/token");

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("archive-payload"),
                }
            );
        });

        var targetFilePath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid()}.rar");

        // Act
        await apiClient.DownloadFileAsync(
            config: config,
            fileUrl: "https://1fichier.com/?abc123",
            targetFilePath: targetFilePath,
            progress: NullTransferProgress.Instance,
            expectedSizeBytes: null,
            cancellationToken: CancellationToken.None
        );

        // Assert
        (await File.ReadAllTextAsync(targetFilePath)).ShouldBe("archive-payload");
        httpMessageHandler.PendingRequests.ShouldBe(0);

        File.Delete(targetFilePath);
    }

    [Test]
    public async Task DownloadFileAsync_TokenEndpointRateLimited_RetriesUntilTokenIsIssued()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        var attempt = 0;

        apiMock
            .Setup(x =>
                x.GetDownloadTokenAsync(
                    "Bearer api-key",
                    It.IsAny<DownloadTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() =>
            {
                attempt++;

                return attempt == 1
                    ? CreateApiResponse<DownloadTokenResponse>(
                        HttpStatusCode.TooManyRequests,
                        content: null
                    )
                    : CreateApiResponse(
                        HttpStatusCode.OK,
                        new DownloadTokenResponse
                        {
                            Status = "OK",
                            Url = "https://node.1fichier.test/token",
                        }
                    );
            });

        httpMessageHandler.Enqueue(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("archive-payload"),
                }
            )
        );

        var targetFilePath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid()}.rar");

        // Act
        await apiClient.DownloadFileAsync(
            config: config,
            fileUrl: "https://1fichier.com/?abc123",
            targetFilePath: targetFilePath,
            progress: NullTransferProgress.Instance,
            expectedSizeBytes: null,
            cancellationToken: CancellationToken.None
        );

        // Assert
        attempt.ShouldBe(2);
        (await File.ReadAllTextAsync(targetFilePath)).ShouldBe("archive-payload");

        File.Delete(targetFilePath);
    }

    [Test]
    public async Task DownloadFileAsync_TokenEndpointReturnsKo_ThrowsAndLeavesNoPartFile()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        apiMock
            .Setup(x =>
                x.GetDownloadTokenAsync(
                    "Bearer api-key",
                    It.IsAny<DownloadTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateApiResponse(
                    HttpStatusCode.OK,
                    new DownloadTokenResponse { Status = "KO", Message = "Resource not found" }
                )
            );

        var targetFilePath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid()}.rar");

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.DownloadFileAsync(
                config: config,
                fileUrl: "https://1fichier.com/?abc123",
                targetFilePath: targetFilePath,
                progress: NullTransferProgress.Instance,
                expectedSizeBytes: null,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("Resource not found");
        File.Exists(targetFilePath).ShouldBeFalse();
        File.Exists(targetFilePath + ".part").ShouldBeFalse();
    }

    [Test]
    public async Task GetFileInfosAsync_CheckLinksResponse_ReturnsFileNameAndSizeOfOnlineLinksAndOfflineForDeleted()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        string[] fileUrls = ["https://1fichier.com/?online", "https://1fichier.com/?deleted"];

        httpMessageHandler.Enqueue(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        https://1fichier.com/?deleted;;;NOT FOUND
                        https://1fichier.com/?online;archive;v2.part1.rar;1050673174

                        """
                    ),
                }
            )
        );

        // Act
        var result = await apiClient.GetFileInfosAsync(config, fileUrls, CancellationToken.None);

        // Assert
        result[fileUrls[0]]
            .ShouldBe(
                new HostedFileInfo(
                    IsOnline: true,
                    FileName: "archive;v2.part1.rar",
                    SizeBytes: 1050673174
                )
            );
        result[fileUrls[1]]
            .ShouldBe(new HostedFileInfo(IsOnline: false, FileName: null, SizeBytes: null));
    }

    [Test]
    public async Task GetFolderContentStatesAsync_FilesSubFoldersAndFailingRequest_MapsStatesAndOmitsFailedFolder()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        SetupRootFolderList(23703607, 23703608, 23703609, 23703610);
        SetupFolderList(
            23703607,
            """{"folder_id":23703607,"files":0,"name":"Empty.Release_ArchiveId_1","create_date":"2026-11-26 01:08:13","pass":0,"status":"OK","size":0}"""
        );
        SetupFolderList(
            23703608,
            """{"folder_id":23703608,"files":2,"name":"Files.Release_ArchiveId_2","create_date":"2026-11-26 01:08:14","pass":0,"status":"OK","size":2048}"""
        );
        SetupFolderList(
            23703609,
            """{"folder_id":23703609,"files":0,"name":"Sub.Folder.Release_ArchiveId_3","create_date":"2026-11-26 01:08:15","pass":0,"status":"OK","size":0,"sub_folders":[{"pass":0,"create_date":"2026-11-26 01:08:16","name":"sub-folder","id":23703611}]}"""
        );

        apiMock
            .Setup(x =>
                x.GetFolderListAsync(
                    "Bearer api-key",
                    It.Is<FolderListRequest>(request => request.FolderId == 23703610),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("1fichier folder list failed"));

        // Act
        var result = await apiClient.GetFolderContentStatesAsync(
            config: config,
            folderIds: ["23703607", "23703608", "23703609", "23703610"],
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new HosterFolderContentStateDto("23703607", HosterFolderContentState.Empty),
            new HosterFolderContentStateDto("23703608", HosterFolderContentState.NotEmpty),
            new HosterFolderContentStateDto("23703609", HosterFolderContentState.NotEmpty),
        ]);
    }

    [Test]
    public async Task GetFolderContentStatesAsync_FolderNotFoundAndOtherError_MapsNotFoundAndOmitsOtherError()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        var folderNotFoundResponse = await CreateErrorApiResponseAsync<DeleteFolderResponse>(
            HttpStatusCode.NotFound,
            """{"status":"KO","message":"Folder not found #236"}"""
        );

        var serverErrorResponse = await CreateErrorApiResponseAsync<DeleteFolderResponse>(
            HttpStatusCode.InternalServerError,
            """{"status":"KO","message":"Other error"}"""
        );

        SetupRootFolderList(23703607, 23703608);

        apiMock
            .Setup(x =>
                x.GetFolderListAsync(
                    "Bearer api-key",
                    It.Is<FolderListRequest>(request => request.FolderId == 23703607),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(folderNotFoundResponse.Error!);

        apiMock
            .Setup(x =>
                x.GetFolderListAsync(
                    "Bearer api-key",
                    It.Is<FolderListRequest>(request => request.FolderId == 23703608),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(serverErrorResponse.Error!);

        // Act
        var result = await apiClient.GetFolderContentStatesAsync(
            config: config,
            folderIds: ["23703607", "23703608"],
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new HosterFolderContentStateDto("23703607", HosterFolderContentState.NotFound),
        ]);
    }

    [Test]
    public async Task GetFolderContentStatesAsync_FolderMissingInRootFolderList_MapsNotFoundWithoutRequestingFolder()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        SetupRootFolderList(23703607);
        SetupFolderList(
            23703607,
            """{"folder_id":23703607,"files":0,"name":"Empty.Release","create_date":"2026-11-26 01:08:13","pass":0,"status":"OK","size":0}"""
        );

        // Act
        var firstResult = await apiClient.GetFolderContentStatesAsync(
            config: config,
            folderIds: ["23703607", "21561843"],
            cancellationToken: CancellationToken.None
        );
        var secondResult = await apiClient.GetFolderContentStatesAsync(
            config: config,
            folderIds: ["21561704"],
            cancellationToken: CancellationToken.None
        );

        // Assert
        firstResult.ShouldBe([
            new HosterFolderContentStateDto("23703607", HosterFolderContentState.Empty),
            new HosterFolderContentStateDto("21561843", HosterFolderContentState.NotFound),
        ]);
        secondResult.ShouldBe([
            new HosterFolderContentStateDto("21561704", HosterFolderContentState.NotFound),
        ]);
        apiMock.Verify(
            x =>
                x.GetFolderListAsync(
                    "Bearer api-key",
                    It.IsAny<FolderListRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    public async Task GetFolderContentStatesAsync_FolderListBlocked_ThrowsWithoutRequestingFurtherFolders(
        HttpStatusCode blockedStatusCode
    )
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        var blockedResponse = await CreateErrorApiResponseAsync<DeleteFolderResponse>(
            blockedStatusCode,
            """{"status":"KO","message":"Blocked"}"""
        );

        SetupRootFolderList(23703607, 23703608);

        apiMock
            .Setup(x =>
                x.GetFolderListAsync(
                    "Bearer api-key",
                    It.Is<FolderListRequest>(request => request.FolderId == 23703607),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(blockedResponse.Error!);

        // Act
        var getFolderContentStatesAsync = () =>
            apiClient.GetFolderContentStatesAsync(
                config: config,
                folderIds: ["23703607", "23703608"],
                cancellationToken: CancellationToken.None
            );

        // Assert
        await Should.ThrowAsync<HttpRequestException>(getFolderContentStatesAsync);
        apiMock.Verify(
            x =>
                x.GetFolderListAsync(
                    "Bearer api-key",
                    It.Is<FolderListRequest>(request => request.FolderId == 23703608),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task GetFolderContentStatesAsync_AccountLocked_ReturnsNoStatesAndRequestsRootFolderListOnce()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        var accountLockedResponse = await CreateErrorApiResponseAsync<DeleteFolderResponse>(
            HttpStatusCode.Forbidden,
            """{"status":"KO","message":"Account locked #211"}"""
        );

        apiMock
            .Setup(x =>
                x.GetFolderListAsync(
                    "Bearer api-key",
                    It.Is<FolderListRequest>(request => request.FolderId == 0),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(accountLockedResponse.Error!);

        // Act
        var firstResult = await apiClient.GetFolderContentStatesAsync(
            config: config,
            folderIds: ["23703607"],
            cancellationToken: CancellationToken.None
        );
        var secondResult = await apiClient.GetFolderContentStatesAsync(
            config: config,
            folderIds: ["23703608"],
            cancellationToken: CancellationToken.None
        );

        // Assert
        firstResult.ShouldBeEmpty();
        secondResult.ShouldBeEmpty();
        apiMock.Verify(
            x =>
                x.GetFolderListAsync(
                    "Bearer api-key",
                    It.IsAny<FolderListRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task DeleteFolderAsync_Success_ReturnsWithoutThrowing()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        SetupDeleteFolder(
            CreateApiResponse(
                HttpStatusCode.OK,
                JsonSerializer.Deserialize<DeleteFolderResponse>(
                    """{"status":"OK","message":"Folder removed #389"}""",
                    JsonSerializerOptions
                )
            )
        );

        // Act
        await apiClient.DeleteFolderAsync(config, "23703607", CancellationToken.None);

        // Assert
        apiMock.Verify(
            x =>
                x.DeleteFolderAsync(
                    "Bearer api-key",
                    It.Is<DeleteFolderRequest>(request => request.FolderId == 23703607),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task DeleteFolderAsync_FolderNotFound_ReturnsWithoutThrowing()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        SetupDeleteFolder(
            await CreateErrorApiResponseAsync<DeleteFolderResponse>(
                HttpStatusCode.NotFound,
                """{"message":"Folder not found #236","status":"KO"}"""
            )
        );

        // Act
        await apiClient.DeleteFolderAsync(config, "23703607", CancellationToken.None);

        // Assert
        apiMock.Verify(
            x =>
                x.DeleteFolderAsync(
                    "Bearer api-key",
                    It.IsAny<DeleteFolderRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task DeleteFolderAsync_NotFoundWithOtherMessage_ThrowsWithMessage()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        SetupDeleteFolder(
            await CreateErrorApiResponseAsync<DeleteFolderResponse>(
                HttpStatusCode.NotFound,
                """{"message":"Resource not found","status":"KO"}"""
            )
        );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            apiClient.DeleteFolderAsync(config, "23703607", CancellationToken.None)
        );

        // Assert
        exception.Message.ShouldBe("Resource not found");
    }

    private void SetupRootFolderList(params int[] subFolderIds)
    {
        var subFoldersJson = string.Join(
            ",",
            subFolderIds.Select(subFolderId =>
                $$"""{"id":{{subFolderId}},"name":"Folder.{{subFolderId}}","create_date":"2026-11-26 01:08:13","pass":0}"""
            )
        );

        SetupFolderList(
            0,
            $$"""{"folder_id":0,"files":0,"name":"","status":"OK","sub_folders":[{{subFoldersJson}}]}"""
        );
    }

    private void SetupFolderList(int folderId, string json)
    {
        apiMock
            .Setup(x =>
                x.GetFolderListAsync(
                    "Bearer api-key",
                    It.Is<FolderListRequest>(request => request.FolderId == folderId),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                JsonSerializer.Deserialize<FolderListResponse>(json, JsonSerializerOptions)!
            );
    }

    private void SetupDeleteFolder(ApiResponse<DeleteFolderResponse> response)
    {
        apiMock
            .Setup(x =>
                x.DeleteFolderAsync(
                    "Bearer api-key",
                    It.Is<DeleteFolderRequest>(request => request.FolderId == 23703607),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(response);
    }

    private static ApiResponse<T> CreateApiResponse<T>(HttpStatusCode statusCode, T? content)
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
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.1fichier.com/v1/test");
        var response = new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = new StringContent(content),
        };
        var settings = new RefitSettings();
        var error = await ApiException.Create(request, HttpMethod.Post, response, settings);

        return new ApiResponse<T>(response, default, settings, error);
    }

    private static HttpResponseMessage CreateJsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") },
            },
        };
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> responses = [];

        public int PendingRequests => responses.Count;

        public void Enqueue(Func<HttpRequestMessage, Task<HttpResponseMessage>> response)
        {
            responses.Enqueue(response);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            responses.Count.ShouldBeGreaterThan(0);
            return responses.Dequeue()(request);
        }
    }
}
