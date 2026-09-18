using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Uploady;
using Bearcat.Hosters.Uploady.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Uploady;

public class UploadyDownloadTest
{
    private Mock<IUploadyApiClient> apiClientMock = null!;
    private Hosters.Uploady.Uploady service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IUploadyApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.Uploady.Uploady>>();
        service = new Hosters.Uploady.Uploady(apiClientMock.Object, loggerMock.Object);
    }

    [Test]
    public void DownloadRequiresPremium_AnonymousDirectLinks_IsFalse()
    {
        // Arrange
        // Act
        var requiresPremium = service.DownloadRequiresPremium;

        // Assert
        requiresPremium.ShouldBeFalse();
    }

    [Test]
    public async Task DownloadFileAsync_FileUrlWithHtmlExtension_ForwardsExtractedFileCode()
    {
        // Arrange
        var config = new UploadyConfig { ApiKey = "api-key" };
        var targetFilePath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid()}.rar");

        apiClientMock
            .Setup(x =>
                x.DownloadFileAsync(
                    "api-key",
                    "abc123",
                    targetFilePath,
                    NullDownloadProgress.Instance,
                    2048L,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.DownloadFileAsync(
            new DownloadFileDto(
                HosterFileLink: "https://uploady.io/abc123.html",
                ExternalId: null,
                ExpectedSizeBytes: 2048
            ),
            targetFilePath,
            config,
            NullDownloadProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessages.ShouldBeEmpty();
        apiClientMock.VerifyAll();
    }

    [Test]
    public async Task DownloadFileAsync_ApiClientThrows_ReturnsFailureWithMessage()
    {
        // Arrange
        var config = new UploadyConfig { ApiKey = "api-key" };
        var targetFilePath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid()}.rar");

        apiClientMock
            .Setup(x =>
                x.DownloadFileAsync(
                    "api-key",
                    "abc123",
                    targetFilePath,
                    NullDownloadProgress.Instance,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("File not found"));

        // Act
        var result = await service.DownloadFileAsync(
            new DownloadFileDto(
                HosterFileLink: "https://uploady.io/abc123.html",
                ExternalId: null,
                ExpectedSizeBytes: null
            ),
            targetFilePath,
            config,
            NullDownloadProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessages.ShouldBe(["File not found"]);
    }

    [Test]
    public async Task GetFileSizesAsync_ClientReturnsSizes_MapsThemBackToFileUrls()
    {
        // Arrange
        var config = new UploadyConfig { ApiKey = "api-key" };
        var firstFileUrl = "https://uploady.io/abc123.html";
        var secondFileUrl = "https://uploady.io/def456.html";

        apiClientMock
            .Setup(x =>
                x.GetFileSizesAsync(
                    "api-key",
                    It.Is<IReadOnlySet<string>>(codes =>
                        codes.Count == 2 && codes.Contains("abc123") && codes.Contains("def456")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<string, long> { ["abc123"] = 2097152 });

        // Act
        var result = await service.GetFileSizesAsync(
            [firstFileUrl, secondFileUrl],
            config,
            CancellationToken.None
        );

        // Assert
        result[firstFileUrl].ShouldBe(2097152);
        result.ShouldNotContainKey(secondFileUrl);
    }

    [Test]
    public async Task GetFileSizesAsync_ClientThrows_ReturnsEmptyDictionary()
    {
        // Arrange
        var config = new UploadyConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x =>
                x.GetFileSizesAsync(
                    "api-key",
                    It.IsAny<IReadOnlySet<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("Not enabled"));

        // Act
        var result = await service.GetFileSizesAsync(
            ["https://uploady.io/abc123.html"],
            config,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }
}
