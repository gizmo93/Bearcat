using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.FreeDlink;
using Bearcat.Hosters.FreeDlink.Api;
using Bearcat.Hosters.Shared.XFilesharing.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.FreeDlink;

public class FreeDlinkTest
{
    private readonly List<string> temporaryFiles = [];
    private Mock<IFreeDlinkApiClient> apiClientMock = null!;
    private Hosters.FreeDlink.FreeDlink service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IFreeDlinkApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.FreeDlink.FreeDlink>>();
        service = new Hosters.FreeDlink.FreeDlink(apiClientMock.Object, loggerMock.Object);
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
    public async Task UploadFileAsync_ApiUploadSucceeds_ReturnsFreeDlinkDownloadUrl()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 21, FullFileName: filePath, UploadId: 121);
        var config = new FreeDlinkConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.RequestUploadAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new RequestUploadResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    UploadUrl = "https://e2.urleecher.com/cgi-bin/upload.cgi?utype=reg",
                    SessionId = "session-id",
                }
            );

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    "https://e2.urleecher.com/cgi-bin/upload.cgi?utype=reg",
                    "session-id",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new UploadFileResponse { FileCode = "38p44fzqqs8y", FileStatus = "OK" });

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
        result.FileUrl.ShouldBe("https://freedl.ink/38p44fzqqs8y.html");
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task UploadFileAsync_FolderIdIsSet_MovesUploadedFileIntoFolder()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 22, FullFileName: filePath, UploadId: 122, FolderId: "16149");
        var config = new FreeDlinkConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.RequestUploadAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new RequestUploadResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    UploadUrl = "https://e2.urleecher.com/cgi-bin/upload.cgi",
                    SessionId = "session-id",
                }
            );

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new UploadFileResponse { FileCode = "38p44fzqqs8y", FileStatus = "OK" });

        apiClientMock
            .Setup(x =>
                x.SetFileFolderAsync(
                    "api-key",
                    "38p44fzqqs8y",
                    "16149",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

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
                x.SetFileFolderAsync(
                    "api-key",
                    "38p44fzqqs8y",
                    "16149",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task CheckFilesExistAsync_FileUrlWithHtmlExtension_MapsStatusToOriginalUrl()
    {
        // Arrange
        var config = new FreeDlinkConfig { ApiKey = "api-key" };
        var fileUrl = "https://freedl.ink/38p44fzqqs8y.html";

        apiClientMock
            .Setup(x =>
                x.FilesExistAsync(
                    "api-key",
                    It.Is<IReadOnlySet<string>>(codes =>
                        codes.Count == 1 && codes.Contains("38p44fzqqs8y")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<string, XFilesharingFileStatus>
                {
                    ["38p44fzqqs8y"] = new(Exists: true, DownloadCount: 4),
                }
            );

        // Act
        var result = await service.CheckFilesExistAsync(
            config,
            [new FileUrlToCheckDto(fileUrl, null)],
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl[fileUrl].ShouldBeTrue();
        result.DownloadCountPerFileUrl.ShouldNotBeNull()[fileUrl].ShouldBe(4);
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task TryLoginAsync_ApiReturnsOk_ReturnsSuccess()
    {
        // Arrange
        var config = new FreeDlinkConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.GetAccountInfoAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountInfoResponse { Status = (int)HttpStatusCode.OK });

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public void DeserializeHosterConfig_SerializedConfig_ReturnsFreeDlinkConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(new FreeDlinkConfig { ApiKey = "api-key" });

        // Act
        var result = service.DeserializeHosterConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<FreeDlinkConfig>().ApiKey.ShouldBe("api-key");
    }

    private string CreateTemporaryFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        File.WriteAllText(filePath, content);
        temporaryFiles.Add(filePath);
        return filePath;
    }
}
