using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Rapidgator;
using Bearcat.Hosters.Rapidgator.Api;
using Bearcat.Hosters.Rapidgator.Api.File;
using Bearcat.Hosters.Rapidgator.Api.User;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Rapidgator;

public class RapidgatorTest
{
    private readonly List<string> temporaryFiles = [];
    private Mock<IRapidgatorApiClient> apiClientMock = null!;
    private Mock<IRapidgatorApi> rapidgatorApiMock = null!;
    private Hosters.Rapidgator.Rapidgator service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IRapidgatorApiClient>(MockBehavior.Strict);
        rapidgatorApiMock = new Mock<IRapidgatorApi>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.Rapidgator.Rapidgator>>();
        service = new Hosters.Rapidgator.Rapidgator(
            apiClientMock.Object,
            rapidgatorApiMock.Object,
            loggerMock.Object
        );
        service.UploadRetryDelay = TimeSpan.Zero;
        service.UploadStatusPollDelay = TimeSpan.Zero;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var temporaryFile in temporaryFiles.Where(File.Exists))
        {
            File.Delete(temporaryFile);
        }
    }

    [Test]
    public void SupportsPremiumOnlyDownloads_ReturnsTrue()
    {
        service.SupportsPremiumOnlyDownloads.ShouldBeTrue();
    }

    [Test]
    public async Task UploadFileAsync_UploadCompletes_ReturnsShortenedFileUrl()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(
            Id: 42,
            FullFileName: filePath,
            UploadId: 142,
            FolderId: "folder-id"
        );
        var config = new RapidgatorConfig { Username = "user", Password = "password" };

        apiClientMock
            .Setup(x =>
                x.RequestUploadFileAsync(
                    Path.GetFileName(filePath),
                    new FileInfo(filePath).Length,
                    It.IsAny<string>(),
                    "folder-id",
                    config,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Response = new UploadFileResponse.ResponseObject
                    {
                        Upload = new UploadFileResponse.Upload
                        {
                            UploadId = "upload-id",
                            Url = "https://upload.rapidgator.test",
                            State = UploadStates.Uploading,
                        },
                    },
                }
            );

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    "https://upload.rapidgator.test",
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
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

        apiClientMock
            .Setup(x => x.GetUploadInfoAsync(config, "upload-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Response = new UploadFileResponse.ResponseObject
                    {
                        Upload = new UploadFileResponse.Upload
                        {
                            UploadId = "upload-id",
                            State = UploadStates.Done,
                            File = new UploadFileResponse.File
                            {
                                FileId = "file-id",
                                Name = Path.GetFileName(filePath),
                                Url =
                                    $"https://rapidgator.net/file/file-id/{Path.GetFileName(filePath)}.html",
                            },
                        },
                    },
                }
            );

        // Act
        var result = await service.UploadFileAsync(
            fileDto,
            config,
            NullUploadProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe("https://rapidgator.net/file/file-id");
        result.ErrorMessages.ShouldBeEmpty();
        apiClientMock.Verify(
            x =>
                x.ChangeFileModeAsync(
                    It.IsAny<RapidgatorConfig>(),
                    It.IsAny<string>(),
                    It.IsAny<UploadMode>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task UploadFileAsync_PremiumOnlyDownload_RequestsPremiumOnlyMode()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(
            Id: 44,
            FullFileName: filePath,
            UploadId: 144,
            PremiumOnlyDownload: true
        );
        var config = new RapidgatorConfig { Username = "user", Password = "password" };

        apiClientMock
            .Setup(x =>
                x.RequestUploadFileAsync(
                    Path.GetFileName(filePath),
                    new FileInfo(filePath).Length,
                    It.IsAny<string>(),
                    null,
                    config,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Response = new UploadFileResponse.ResponseObject
                    {
                        Upload = new UploadFileResponse.Upload
                        {
                            UploadId = "upload-id",
                            Url = "https://upload.rapidgator.test",
                            State = UploadStates.Uploading,
                        },
                    },
                }
            );

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    "https://upload.rapidgator.test",
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
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

        apiClientMock
            .Setup(x => x.GetUploadInfoAsync(config, "upload-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Response = new UploadFileResponse.ResponseObject
                    {
                        Upload = new UploadFileResponse.Upload
                        {
                            UploadId = "upload-id",
                            State = UploadStates.Done,
                            File = new UploadFileResponse.File
                            {
                                FileId = "file-id",
                                Name = Path.GetFileName(filePath),
                                Url =
                                    $"https://rapidgator.net/file/file-id/{Path.GetFileName(filePath)}.html",
                            },
                        },
                    },
                }
            );

        apiClientMock
            .Setup(x =>
                x.ChangeFileModeAsync(
                    config,
                    "file-id",
                    UploadMode.PremiumOnly,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFileResponse
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
                }
            );

        // Act
        var result = await service.UploadFileAsync(
            fileDto,
            config,
            NullUploadProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        apiClientMock.Verify(
            x =>
                x.ChangeFileModeAsync(
                    config,
                    "file-id",
                    UploadMode.PremiumOnly,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task UploadFileAsync_RequestUploadFails_ReturnsFailureAndRetriesThreeTimes()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 43, FullFileName: filePath, UploadId: 143);
        var config = new RapidgatorConfig { Username = "user", Password = "password" };

        apiClientMock
            .Setup(x =>
                x.RequestUploadFileAsync(
                    Path.GetFileName(filePath),
                    new FileInfo(filePath).Length,
                    It.IsAny<string>(),
                    null,
                    config,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Status = (int)HttpStatusCode.ServiceUnavailable,
                    Details = "temporary upload error",
                    Response = new UploadFileResponse.ResponseObject
                    {
                        Upload = new UploadFileResponse.Upload
                        {
                            UploadId = "upload-id",
                            StateLabel = "temporary upload error",
                        },
                    },
                }
            );

        // Act
        var result = await service.UploadFileAsync(
            fileDto,
            config,
            NullUploadProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.FileUrl.ShouldBeNull();
        result.ErrorMessages.ShouldBe([
            "temporary upload error",
            "temporary upload error",
            "temporary upload error",
        ]);
        apiClientMock.Verify(
            x =>
                x.RequestUploadFileAsync(
                    Path.GetFileName(filePath),
                    new FileInfo(filePath).Length,
                    It.IsAny<string>(),
                    null,
                    config,
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(3)
        );
        apiClientMock.Verify(
            x =>
                x.UploadFileAsync(
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task CreateFolderAsync_Config_CreatesFolderWithApiClient()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };

        apiClientMock
            .Setup(x =>
                x.CreateFolderAsync("release-folder", config, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync("folder-id");

        // Act
        var result = await service.CreateFolderAsync(
            "release-folder",
            config,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("folder-id");
    }

    [Test]
    public async Task CheckFilesExistAsync_ApiReturnsStatuses_ReturnsStatuses()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };
        var fileUrls = new[]
        {
            "https://rapidgator.net/file/online",
            "https://rapidgator.net/file/offline",
        };

        apiClientMock
            .Setup(x =>
                x.CheckLinksAsync(
                    config,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<string, LinkCheckStatus>
                {
                    [fileUrls[0]] = new LinkCheckStatus(true, 42),
                    [fileUrls[1]] = new LinkCheckStatus(false, null),
                }
            );

        // Act
        var result = await service.CheckFilesExistAsync(
            config,
            fileUrls.Select(url => new FileUrlToCheckDto(url, null)).ToList(),
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl[fileUrls[0]].ShouldBeTrue();
        result.StatusPerFileUrl[fileUrls[1]].ShouldBeFalse();
        result.DownloadCountPerFileUrl.ShouldNotBeNull();
        result.DownloadCountPerFileUrl[fileUrls[0]].ShouldBe(42);
        result.DownloadCountPerFileUrl.ShouldNotContainKey(fileUrls[1]);
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task CheckFilesExistAsync_ApiThrows_ReturnsFailure()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };
        var fileUrls = new[] { "https://rapidgator.net/file/online" };

        apiClientMock
            .Setup(x =>
                x.CheckLinksAsync(
                    config,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("login failed"));

        // Act
        var result = await service.CheckFilesExistAsync(
            config,
            fileUrls.Select(url => new FileUrlToCheckDto(url, null)).ToList(),
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.StatusPerFileUrl.ShouldBeEmpty();
        result.ErrorMessages.ShouldBe(["Login failed: login failed"]);
    }

    [Test]
    public async Task TryLoginAsync_LoginReturnsOk_ReturnsSuccess()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };

        rapidgatorApiMock
            .Setup(x => x.LoginAsync("user", "password", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateApiResponse(CreateLoginResponse(maxJobs: 8)));

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task TryLoginAsync_LoginReturnsError_ReturnsFailure()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };

        rapidgatorApiMock
            .Setup(x => x.LoginAsync("user", "password", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    CreateLoginResponse(maxJobs: 0, status: 400, details: "invalid credentials")
                )
            );

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("invalid credentials");
    }

    [Test]
    public async Task GetMaximumParallelUploadsAsync_LoginReturnsLimit_ReturnsLimit()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };

        rapidgatorApiMock
            .Setup(x => x.LoginAsync("user", "password", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateApiResponse(CreateLoginResponse(maxJobs: 11)));

        // Act
        var result = await service.GetMaximumParallelUploadsAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(11);
    }

    [Test]
    public void DeserializeHosterConfig_SerializedConfig_ReturnsRapidgatorConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(
            new RapidgatorConfig { Username = "user", Password = "password" }
        );

        // Act
        var result = service.DeserializeHosterConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<RapidgatorConfig>().Username.ShouldBe("user");
        result.ShouldBeOfType<RapidgatorConfig>().Password.ShouldBe("password");
    }

    private string CreateTemporaryFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        File.WriteAllText(filePath, content);
        temporaryFiles.Add(filePath);
        return filePath;
    }

    private static LoginResponse CreateLoginResponse(
        int maxJobs,
        int status = (int)HttpStatusCode.OK,
        string? details = null
    )
    {
        return new LoginResponse
        {
            Status = status,
            Details = details,
            Response = new LoginResponse.ResponseObject
            {
                Token = "token",
                User = new LoginResponse.User
                {
                    RemoteUpload = new LoginResponse.RemoteUpload { MaxNbJobs = maxJobs },
                },
            },
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
