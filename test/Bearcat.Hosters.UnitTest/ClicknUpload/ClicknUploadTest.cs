using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.ClicknUpload;
using Bearcat.Hosters.ClicknUpload.Api;
using Bearcat.Hosters.Shared.XFilesharing.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.ClicknUpload;

public class ClicknUploadTest
{
    private readonly List<string> temporaryFiles = [];
    private Mock<IClicknUploadApiClient> apiClientMock = null!;
    private Hosters.ClicknUpload.ClicknUpload service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IClicknUploadApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.ClicknUpload.ClicknUpload>>();
        service = new Hosters.ClicknUpload.ClicknUpload(apiClientMock.Object, loggerMock.Object);
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
    public async Task UploadFileAsync_ApiUploadSucceeds_ReturnsClicknUploadDownloadUrl()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 17, FullFileName: filePath, UploadId: 117);
        var config = new ClicknUploadConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.RequestUploadAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new RequestUploadResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    UploadUrl = "https://s1.clicknupload.click/upload/01",
                    SessionId = "session-id",
                }
            );

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    "https://s1.clicknupload.click/upload/01",
                    "session-id",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new UploadFileResponse { FileCode = "abc123", FileStatus = "OK" });

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
        result.FileUrl.ShouldBe("https://clicknupload.click/abc123");
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task CheckFilesExistAsync_FileUrl_MapsStatusToOriginalUrl()
    {
        // Arrange
        var config = new ClicknUploadConfig { ApiKey = "api-key" };
        var fileUrl = "https://clicknupload.click/online-code";

        apiClientMock
            .Setup(x =>
                x.FilesExistAsync(
                    "api-key",
                    It.Is<IReadOnlySet<string>>(codes =>
                        codes.Count == 1 && codes.Contains("online-code")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<string, bool> { ["online-code"] = true });

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
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task TryLoginAsync_ApiReturnsOk_ReturnsSuccess()
    {
        // Arrange
        var config = new ClicknUploadConfig { ApiKey = "api-key" };

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
    public void DeserializeHosterConfig_SerializedConfig_ReturnsClicknUploadConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(
            new ClicknUploadConfig { ApiKey = "api-key" }
        );

        // Act
        var result = service.DeserializeHosterConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ClicknUploadConfig>().ApiKey.ShouldBe("api-key");
    }

    private string CreateTemporaryFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        File.WriteAllText(filePath, content);
        temporaryFiles.Add(filePath);
        return filePath;
    }
}
