using System.Text.Json;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.GoFile;
using Bearcat.Hosters.GoFile.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using AccountResponse = Bearcat.Hosters.GoFile.Api.GetAccountId.Response;
using UploadFileData = Bearcat.Hosters.GoFile.Api.UploadFile.Data;
using UploadFileResponse = Bearcat.Hosters.GoFile.Api.UploadFile.Response;

namespace Bearcat.Hosters.UnitTest.GoFile;

public class GoFileTest
{
    private readonly List<string> temporaryFiles = [];
    private Mock<IGoFileApiClient> apiClientMock = null!;
    private Bearcat.Hosters.GoFile.GoFile service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IGoFileApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Bearcat.Hosters.GoFile.GoFile>>();
        service = new Bearcat.Hosters.GoFile.GoFile(apiClientMock.Object, loggerMock.Object);
        service.UploadRetryDelay = TimeSpan.Zero;
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
        var fileDto = new FileDto(Id: 23, FullFileName: filePath);
        var config = new GoFileConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    "api-key",
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Status = "ok",
                    Data = new UploadFileData { DownloadUrl = "https://gofile.io/d/file-id" },
                }
            );

        // Act
        var result = await service.UploadFileAsync(fileDto, config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe("https://gofile.io/d/file-id");
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task UploadFileAsync_ApiUploadThrows_ReturnsFailureAndRetriesThreeTimes()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 24, FullFileName: filePath);
        var config = new GoFileConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    "api-key",
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("upload failed"));

        // Act
        var result = await service.UploadFileAsync(fileDto, config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.FileUrl.ShouldBeNull();
        result.ErrorMessages.ShouldBe(["upload failed"]);
        apiClientMock.Verify(
            x =>
                x.UploadFileAsync(
                    "api-key",
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(3)
        );
    }

    [Test]
    public async Task CheckFilesExistAsync_ApiReturnsMixedStatuses_ReturnsStatusesAndErrors()
    {
        // Arrange
        var config = new GoFileConfig { ApiKey = "api-key" };
        var fileUrls = new[] { "https://gofile.io/d/online", "https://gofile.io/d/error" };

        apiClientMock
            .Setup(x =>
                x.CheckOnlineStatusAsync(fileUrls, "api-key", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Dictionary<string, (bool IsOnline, string? ErrorMessage)>
                {
                    [fileUrls[0]] = (true, null),
                    [fileUrls[1]] = (false, "api-error"),
                }
            );

        // Act
        var result = await service.CheckFilesExistAsync(config, fileUrls, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.StatusPerFileUrl[fileUrls[0]].ShouldBeTrue();
        result.StatusPerFileUrl[fileUrls[1]].ShouldBeFalse();
        result.ErrorMessages.ShouldBe(["api-error"]);
    }

    [Test]
    public async Task TryLoginAsync_ApiReturnsOk_ReturnsSuccess()
    {
        // Arrange
        var config = new GoFileConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.GetAccountAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountResponse { Status = "ok" });

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task TryLoginAsync_ApiReturnsError_ReturnsFailure()
    {
        // Arrange
        var config = new GoFileConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.GetAccountAsync("api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountResponse { Status = "error-invalidToken" });

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("error-invalidToken");
    }

    [Test]
    public async Task GetMaximumParallelUploadsAsync_Config_ReturnsStaticLimit()
    {
        // Arrange
        var config = new GoFileConfig { ApiKey = "api-key" };

        // Act
        var result = await service.GetMaximumParallelUploadsAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(100);
    }

    [Test]
    public void DeserializeHosterConfig_SerializedConfig_ReturnsGoFileConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(new GoFileConfig { ApiKey = "api-key" });

        // Act
        var result = service.DeserializeHosterConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<GoFileConfig>().ApiKey.ShouldBe("api-key");
    }

    private string CreateTemporaryFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        File.WriteAllText(filePath, content);
        temporaryFiles.Add(filePath);
        return filePath;
    }
}
