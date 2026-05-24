using System.Text.Json;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Fichier;
using Bearcat.Hosters.Fichier.Api;
using Bearcat.Hosters.Fichier.Api.Upload;
using Bearcat.Hosters.Fichier.Api.User;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Fichier;

public class FichierTest
{
    private readonly List<string> temporaryFiles = [];
    private Mock<IFichierApiClient> apiClientMock = null!;
    private Hosters.Fichier.Fichier service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IFichierApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.Fichier.Fichier>>();
        service = new Hosters.Fichier.Fichier(apiClientMock.Object, loggerMock.Object)
        {
            UploadRetryDelay = TimeSpan.Zero,
        };
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
        var fileDto = new FileDto(Id: 31, FullFileName: filePath);
        var config = new FichierConfig { ApiKey = "api-key" };

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
                new EndUploadResponse
                {
                    Links =
                    [
                        new EndUploadResponse.UploadedLink
                        {
                            Download = "https://1fichier.com/?download",
                            Filename = Path.GetFileName(filePath),
                            Size = "14",
                        },
                    ],
                }
            );

        // Act
        var result = await service.UploadFileAsync(fileDto, config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe("https://1fichier.com/?download");
        result.ErrorMessages.ShouldBeEmpty();
    }

    [Test]
    public async Task UploadFileAsync_ApiUploadThrows_ReturnsFailureAndRetriesThreeTimes()
    {
        // Arrange
        var filePath = CreateTemporaryFile("upload-content");
        var fileDto = new FileDto(Id: 32, FullFileName: filePath);
        var config = new FichierConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    config,
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
        result.ErrorMessages.ShouldBe(["upload failed", "upload failed", "upload failed"]);
        apiClientMock.Verify(
            x =>
                x.UploadFileAsync(
                    config,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(3)
        );
    }

    [Test]
    public async Task CheckFilesExistAsync_ApiReturnsStatuses_ReturnsSuccess()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };
        var fileUrls = new[]
        {
            "https://1fichier.com/?online",
            "https://1fichier.com/?offline",
        };

        apiClientMock
            .Setup(x => x.CheckLinksAsync(config, fileUrls, It.IsAny<CancellationToken>()))
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
        var config = new FichierConfig { ApiKey = "api-key" };
        var fileUrls = new[] { "https://1fichier.com/?file" };

        apiClientMock
            .Setup(x => x.CheckLinksAsync(config, fileUrls, It.IsAny<CancellationToken>()))
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
    public async Task TryLoginAsync_UserInfoHasEmail_ReturnsSuccess()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.GetUserInfoAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserInfoResponse { Email = "user@example.test" });

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task TryLoginAsync_UserInfoReturnsKo_ReturnsFailure()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.GetUserInfoAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserInfoResponse { Status = "KO", Message = "invalid key" });

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("invalid key");
    }

    [Test]
    public async Task GetMaximumParallelUploadsAsync_Config_ReturnsStaticLimit()
    {
        // Arrange
        var config = new FichierConfig { ApiKey = "api-key" };

        // Act
        var result = await service.GetMaximumParallelUploadsAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(3);
    }

    [Test]
    public void DeserializeHosterConfig_SerializedConfig_ReturnsFichierConfig()
    {
        // Arrange
        var serializedConfig = JsonSerializer.Serialize(new FichierConfig { ApiKey = "api-key" });

        // Act
        var result = service.DeserializeHosterConfig(serializedConfig);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<FichierConfig>().ApiKey.ShouldBe("api-key");
    }

    private string CreateTemporaryFile(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        File.WriteAllText(filePath, content);
        temporaryFiles.Add(filePath);
        return filePath;
    }
}
