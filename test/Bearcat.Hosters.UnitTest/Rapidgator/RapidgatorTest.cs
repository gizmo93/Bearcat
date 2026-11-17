using System.Net;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Rapidgator;
using Bearcat.Hosters.Rapidgator.Api;
using Bearcat.Hosters.Rapidgator.Api.File;
using Bearcat.Hosters.Rapidgator.Api.User;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Rapidgator;

public class RapidgatorTest
{
    private readonly List<string> temporaryFiles = [];
    private Mock<IRapidgatorApiClient> apiClientMock = null!;
    private Hosters.Rapidgator.Rapidgator service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IRapidgatorApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.Rapidgator.Rapidgator>>();
        service = new Hosters.Rapidgator.Rapidgator(apiClientMock.Object, loggerMock.Object);
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
    public async Task UploadFileAsync_StatusContainsErrorAndUploadedFile_ReturnsSuccess()
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
                    Status = (int)HttpStatusCode.InternalServerError,
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
                        State = UploadStates.Processing,
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
                            State = UploadStates.Failed,
                            StateLabel = "Fail",
                            Error = new UploadFileResponse.UploadError
                            {
                                Code = 500,
                                Message =
                                    "Specified argument was out of the range of valid values.",
                            },
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
            NullTransferProgress.Instance,
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
    public async Task UploadFileAsync_StatusFailsButRetryFindsExistingFile_ReturnsSuccess()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileName = Path.GetFileName(filePath);
        var fileDto = new FileDto(Id: 45, FullFileName: filePath, UploadId: 145);
        var config = new RapidgatorConfig { Username = "user", Password = "password" };

        apiClientMock
            .SetupSequence(x =>
                x.RequestUploadFileAsync(
                    fileName,
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
            )
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Response = new UploadFileResponse.ResponseObject
                    {
                        Upload = new UploadFileResponse.Upload
                        {
                            State = UploadStates.Done,
                            File = new UploadFileResponse.File
                            {
                                FileId = "file-id",
                                Name = fileName,
                                Url = $"https://rapidgator.net/file/file-id/{fileName}.html",
                            },
                        },
                    },
                }
            );

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    "https://upload.rapidgator.test",
                    It.IsAny<Stream>(),
                    fileName,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Status = (int)HttpStatusCode.InternalServerError,
                    Response = new UploadFileResponse.ResponseObject
                    {
                        Upload = new UploadFileResponse.Upload
                        {
                            UploadId = "upload-id",
                            State = UploadStates.Processing,
                        },
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
                            State = UploadStates.Failed,
                            StateLabel = "Failed",
                            Error = new UploadFileResponse.UploadError
                            {
                                Code = 500,
                                Message = "Processing failed",
                            },
                        },
                    },
                }
            );

        // Act
        var result = await service.UploadFileAsync(
            fileDto,
            config,
            NullTransferProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe("https://rapidgator.net/file/file-id");
        result.ErrorMessages.ShouldBeEmpty();
        apiClientMock.Verify(
            x =>
                x.RequestUploadFileAsync(
                    fileName,
                    new FileInfo(filePath).Length,
                    It.IsAny<string>(),
                    null,
                    config,
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
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
                        Upload = new UploadFileResponse.Upload
                        {
                            UploadId = "upload-id",
                            State = UploadStates.Processing,
                        },
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
            NullTransferProgress.Instance,
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
            NullTransferProgress.Instance,
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
    public async Task CheckFilesExistAsync_CredentialsRejected_ThrowsHosterCredentialsRejected()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };

        apiClientMock
            .Setup(x =>
                x.CheckLinksAsync(
                    config,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new HosterCredentialsRejectedException("Error: Wrong e-mail or password.")
            );

        // Act
        var exception = await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            service.CheckFilesExistAsync(
                config,
                [new FileUrlToCheckDto("https://rapidgator.net/file/online", null)],
                CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldBe("Error: Wrong e-mail or password.");
    }

    [Test]
    public async Task TryLoginAsync_CredentialsRejected_ReturnsFailure()
    {
        // Arrange
        var config = new RapidgatorConfig { Username = "user", Password = "password" };

        apiClientMock
            .Setup(x => x.LoginAsync(config, It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new HosterCredentialsRejectedException("Error: Wrong e-mail or password.")
            );

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Error: Wrong e-mail or password.");
    }

    private string CreateTemporaryFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        File.WriteAllText(filePath, content);
        temporaryFiles.Add(filePath);
        return filePath;
    }

    [TestCase(
        "https://rg.to/file/36520a0eb66e18c296421874a50bd30b/archive.part5.rar.html",
        "https://rapidgator.net/file/36520a0eb66e18c296421874a50bd30b"
    )]
    [TestCase(
        "https://www.rapidgator.net/file/fd90f309156dca5c7b83359483451bba",
        "https://rapidgator.net/file/fd90f309156dca5c7b83359483451bba"
    )]
    [TestCase("https://rapidgator.net/folder/123/name.html", null)]
    public void ConvertToOwnFileLinkFormat_RapidgatorLinkVariants_ReturnsRapidgatorNetLinkWithoutFileName(
        string fileLink,
        string? expectedFileLink
    )
    {
        // Act
        var result = service.ConvertToOwnFileLinkFormat(fileLink);

        // Assert
        result.ShouldBe(expectedFileLink);
    }
}
