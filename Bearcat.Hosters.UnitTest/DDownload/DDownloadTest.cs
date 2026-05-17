using System.Net;
using System.Text.Json;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.DDownload;
using Bearcat.Hosters.DDownload.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using AccountInfoResponse = Bearcat.Hosters.DDownload.Api.AccountInfo.Response;
using RequestUploadResponse = Bearcat.Hosters.DDownload.Api.RequestUpload.Response;
using UploadFileResponse = Bearcat.Hosters.DDownload.Api.UploadFile.Response;

namespace Bearcat.Hosters.UnitTest.DDownload;

public class DDownloadTest
{
    private readonly List<string> temporaryFiles = [];
    private Mock<IDDownloadApiClient> apiClientMock = null!;
    private Bearcat.Hosters.DDownload.DDownload service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IDDownloadApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Bearcat.Hosters.DDownload.DDownload>>();
        service = new Bearcat.Hosters.DDownload.DDownload(apiClientMock.Object, loggerMock.Object);
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
    public async Task UploadFileAsync_ApiUploadSucceeds_ReturnsDownloadUrl()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 17, FullFileName: filePath);
        var config = new DDownloadConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.RequestUploadAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new RequestUploadResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    UploadUrl = "https://upload.ddownload.test",
                    SessionId = "session-id",
                }
            );

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    "https://upload.ddownload.test",
                    "session-id",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new UploadFileResponse { FileCode = "abc123", FileStatus = "OK" });

        // Act
        var result = await service.UploadFileAsync(fileDto, config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe("https://www.ddownload.com/abc123");
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task UploadFileAsync_RequestUploadFails_ReturnsFailureAndRetriesThreeTimes()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 18, FullFileName: filePath);
        var config = new DDownloadConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.RequestUploadAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new RequestUploadResponse
                {
                    Status = (int)HttpStatusCode.InternalServerError,
                    Msg = "temporary upload error",
                }
            );

        // Act
        var result = await service.UploadFileAsync(fileDto, config, CancellationToken.None);

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
            x => x.RequestUploadAsync("api-key", It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );
        apiClientMock.Verify(
            x =>
                x.UploadFileAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task CheckFilesExistAsync_ApiReturnsFileCodeStatuses_MapsStatusesToOriginalUrls()
    {
        // Arrange
        var config = new DDownloadConfig { ApiKey = "api-key" };
        var fileUrls = new[]
        {
            "https://www.ddownload.com/online-code",
            "https://www.ddownload.com/offline-code",
        };

        apiClientMock
            .Setup(x =>
                x.FilesExistAsync(
                    "api-key",
                    It.Is<IReadOnlySet<string>>(codes =>
                        codes.Count == 2
                        && codes.Contains("online-code")
                        && codes.Contains("offline-code")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<string, bool> { ["online-code"] = true, ["offline-code"] = false }
            );

        // Act
        var result = await service.CheckFilesExistAsync(config, fileUrls, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl[fileUrls[0]].ShouldBeTrue();
        result.StatusPerFileUrl[fileUrls[1]].ShouldBeFalse();
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task CheckFilesExistAsync_ApiThrows_ReturnsFailure()
    {
        // Arrange
        var config = new DDownloadConfig { ApiKey = "api-key" };
        var fileUrls = new[] { "https://www.ddownload.com/file-code" };

        apiClientMock
            .Setup(x =>
                x.FilesExistAsync(
                    "api-key",
                    It.IsAny<IReadOnlySet<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("ddownload unavailable"));

        // Act
        var result = await service.CheckFilesExistAsync(config, fileUrls, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.StatusPerFileUrl.ShouldBeEmpty();
        result.ErrorMessages.ShouldBe(["ddownload unavailable"]);
    }

    [Test]
    public async Task TryLoginAsync_ApiReturnsOk_ReturnsSuccess()
    {
        // Arrange
        var config = new DDownloadConfig { ApiKey = "api-key" };

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
    public async Task TryLoginAsync_ApiThrows_ReturnsFailure()
    {
        // Arrange
        var config = new DDownloadConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.GetAccountInfoAsync("api-key", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("login failed"));

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("login failed");
    }

    [Test]
    public async Task GetMaximumParallelUploadsAsync_Config_ReturnsStaticLimit()
    {
        // Arrange
        var config = new DDownloadConfig { ApiKey = "api-key" };

        // Act
        var result = await service.GetMaximumParallelUploadsAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(50);
    }

    [Test]
    public void DeserializeHosterConfig_SerializedConfig_ReturnsDDownloadConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(new DDownloadConfig { ApiKey = "api-key" });

        // Act
        var result = service.DeserializeHosterConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<DDownloadConfig>().ApiKey.ShouldBe("api-key");
    }

    private string CreateTemporaryFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        File.WriteAllText(filePath, content);
        temporaryFiles.Add(filePath);
        return filePath;
    }
}
