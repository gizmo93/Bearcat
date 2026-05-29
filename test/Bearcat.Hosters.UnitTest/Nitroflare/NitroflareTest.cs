using System.Text.Json;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Nitroflare;
using Bearcat.Hosters.Nitroflare.Api;
using Bearcat.Hosters.Nitroflare.Api.File;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Nitroflare;

public class NitroflareTest
{
    private readonly List<string> temporaryFiles = [];
    private Mock<INitroflareApiClient> apiClientMock = null!;
    private Hosters.Nitroflare.Nitroflare service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<INitroflareApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.Nitroflare.Nitroflare>>();
        service = new Hosters.Nitroflare.Nitroflare(apiClientMock.Object, loggerMock.Object);
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
        var fileDto = new FileDto(Id: 41, FullFileName: filePath, UploadId: 141);
        var config = new NitroflareConfig { UserHash = "user-hash" };

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    config,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Files = [new UploadedFile { Url = "https://nitroflare.com/view/file-id" }],
                }
            );

        // Act
        var result = await service.UploadFileAsync(fileDto, config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe("https://nitroflare.com/view/file-id");
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task CheckFilesExistAsync_ApiReturnsStatuses_ReturnsSuccess()
    {
        // Arrange
        var config = new NitroflareConfig { UserHash = "user-hash" };
        var fileUrls = new[]
        {
            "https://nitroflare.com/view/online",
            "https://nitroflare.com/view/offline",
        };

        apiClientMock
            .Setup(x => x.CheckLinksAsync(fileUrls, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Dictionary<string, bool>
                {
                    [fileUrls[0]] = true,
                    [fileUrls[1]] = false,
                }
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
        var config = new NitroflareConfig { UserHash = "user-hash" };
        var fileUrls = new[] { "https://nitroflare.com/view/file-id" };

        apiClientMock
            .Setup(x => x.CheckLinksAsync(fileUrls, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("link check failed"));

        // Act
        var result = await service.CheckFilesExistAsync(config, fileUrls, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.StatusPerFileUrl.ShouldBeEmpty();
        result.ErrorMessages.ShouldBe(["link check failed"]);
    }

    [Test]
    public async Task TryLoginAsync_TestUploadReturnsUrl_ReturnsSuccess()
    {
        // Arrange
        var config = new NitroflareConfig { UserHash = "user-hash" };

        apiClientMock
            .Setup(x => x.TestUserHashAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Files = [new UploadedFile { Url = "https://nitroflare.com/view/test" }],
                }
            );

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task TryLoginAsync_TestUploadReturnsError_ReturnsFailure()
    {
        // Arrange
        var config = new NitroflareConfig { UserHash = "user-hash" };

        apiClientMock
            .Setup(x => x.TestUserHashAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Files = [new UploadedFile { Error = "invalid user hash" }],
                }
            );

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("invalid user hash");
    }

    [Test]
    public async Task GetMaximumParallelUploadsAsync_Config_ReturnsStaticLimit()
    {
        // Arrange
        var config = new NitroflareConfig { UserHash = "user-hash" };

        // Act
        var result = await service.GetMaximumParallelUploadsAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(5);
    }

    [Test]
    public void DeserializeHosterConfig_SerializedConfig_ReturnsNitroflareConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(
            new NitroflareConfig { UserHash = "user-hash" }
        );

        // Act
        var result = service.DeserializeHosterConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<NitroflareConfig>().UserHash.ShouldBe("user-hash");
    }

    private string CreateTemporaryFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        File.WriteAllText(filePath, content);
        temporaryFiles.Add(filePath);
        return filePath;
    }
}
