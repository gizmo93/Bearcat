using System.Text.Json;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.CloudFam;
using Bearcat.Hosters.CloudFam.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.CloudFam;

public class CloudFamTest
{
    private const string FileCode = "b578rni0e1ka";

    private Mock<ICloudFamApiClient> apiClientMock = null!;
    private Hosters.CloudFam.CloudFam service = null!;
    private CloudFamConfig config = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        apiClientMock = new Mock<ICloudFamApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.CloudFam.CloudFam>>();
        service = new Hosters.CloudFam.CloudFam(apiClientMock.Object, loggerMock.Object)
        {
            UploadRetryDelay = TimeSpan.Zero,
        };

        config = new CloudFamConfig { ApiKey = "cloudfam-api-key" };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        await File.WriteAllTextAsync(filePath, "cloudfam test content");
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_UploadSucceeds_ReturnsDownloadLinkAndFileIdAsExternalId()
    {
        // Arrange
        var fileDto = new FileDto(Id: 17, FullFileName: filePath, UploadId: 117, FolderId: "15");

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    config,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    new FileInfo(filePath).Length,
                    "15",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new CloudFamUploadResult("139473", $"https://cloudfam.io/{FileCode}"));

        // Act
        var result = await service.UploadFileAsync(
            fileDto,
            config,
            NullUploadProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe($"https://cloudfam.io/{FileCode}");
        result.ExternalId.ShouldBe("139473");
    }

    [Test]
    public async Task UploadFileAsync_UploadKeepsFailing_ReturnsAllErrorMessages()
    {
        // Arrange
        var fileDto = new FileDto(Id: 17, FullFileName: filePath, UploadId: 117);

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    config,
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("storage unreachable"));

        // Act
        var result = await service.UploadFileAsync(
            fileDto,
            config,
            NullUploadProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.FileUrl.ShouldBeNull();
        result.ErrorMessages.ShouldAllBe(message => message == "storage unreachable");
        result.ErrorMessages.Count.ShouldBe(3);
    }

    [Test]
    public async Task CheckFilesExistAsync_LinksChecked_MapsStatusAndDownloadCount()
    {
        // Arrange
        var fileUrl = $"https://cloudfam.io/{FileCode}";
        var files = new List<FileUrlToCheckDto> { new(fileUrl, ExternalId: null) };

        apiClientMock
            .Setup(x => x.CheckLinksAsync(config, files, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Dictionary<string, LinkCheckStatus>
                {
                    [fileUrl] = new(IsOnline: true, DownloadCount: 12),
                }
            );

        // Act
        var result = await service.CheckFilesExistAsync(config, files, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl[fileUrl].ShouldBeTrue();
        result.DownloadCountPerFileUrl.ShouldNotBeNull()[fileUrl].ShouldBe(12);
    }

    [Test]
    public async Task CheckFilesExistAsync_CheckThrows_ReturnsFailedResult()
    {
        // Arrange
        var files = new List<FileUrlToCheckDto>
        {
            new($"https://cloudfam.io/{FileCode}", ExternalId: null),
        };

        apiClientMock
            .Setup(x => x.CheckLinksAsync(config, files, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Invalid credentials"));

        // Act
        var result = await service.CheckFilesExistAsync(config, files, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessages.ShouldContain("Invalid credentials");
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyRejected_ReturnsInvalidCredentials()
    {
        // Arrange
        apiClientMock
            .Setup(x => x.IsApiKeyValidAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Invalid credentials");
    }

    [Test]
    public void SerializeHosterConfig_ApiKeyGiven_RoundTripsThroughDeserialization()
    {
        // Arrange
        var serialized = service.SerializeHosterConfig(
            new Dictionary<string, string> { [nameof(CloudFamConfig.ApiKey)] = "cloudfam-api-key" }
        );

        // Act
        var result = service.DeserializeHosterConfig(serialized);

        // Assert
        result.ShouldBeOfType<CloudFamConfig>().ApiKey.ShouldBe("cloudfam-api-key");
        JsonSerializer.Deserialize<CloudFamConfig>(serialized).ShouldNotBeNull();
    }

    [Test]
    public async Task CreateFolderAsync_FolderRequested_ReturnsFolderIdFromApiClient()
    {
        // Arrange
        apiClientMock
            .Setup(x => x.CreateFolderAsync(config, "Release.Name", It.IsAny<CancellationToken>()))
            .ReturnsAsync("15");

        // Act
        var result = await service.CreateFolderAsync(
            "Release.Name",
            config,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("15");
    }
}
