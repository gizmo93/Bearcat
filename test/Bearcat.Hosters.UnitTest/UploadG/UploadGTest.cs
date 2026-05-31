using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.UploadG;
using Bearcat.Hosters.UploadG.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.UploadG;

public class UploadGTest
{
    private Mock<IUploadGApiClient> apiClientMock = null!;
    private Hosters.UploadG.UploadG service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IUploadGApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.UploadG.UploadG>>();
        service = new Hosters.UploadG.UploadG(apiClientMock.Object, loggerMock.Object);
        service.UploadRetryDelay = TimeSpan.Zero;
    }

    [Test]
    public async Task UploadFileAsync_FolderId_PassesFolderIdAndReturnsShareableLink()
    {
        // Arrange
        var config = new UploadGConfig { ApiKey = "api-key" };
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, "content");
        var fileInfo = new FileInfo(filePath);
        var fileDto = new FileDto(
            Id: 17,
            FullFileName: filePath,
            UploadId: 117,
            FolderId: "folder-id"
        );

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    config,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    "folder-id",
                    fileInfo.Length,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFileResponse(
                    Status: "success",
                    FileEntry: new FileEntry(19, Path.GetFileName(filePath), null, "text")
                )
            );

        apiClientMock
            .Setup(x =>
                x.GetOrCreateShareableLinkAsync(config, 19, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync("https://uploadg.com/drive/s/hash-value");

        try
        {
            // Act
            var result = await service.UploadFileAsync(fileDto, config, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.FileUrl.ShouldBe("https://uploadg.com/drive/s/hash-value");
            result.ExternalId.ShouldBe("19");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public async Task UploadFileAsync_ApiClientTimeout_RetriesAndReturnsShareableLink()
    {
        // Arrange
        var config = new UploadGConfig { ApiKey = "api-key" };
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, "content");
        var fileInfo = new FileInfo(filePath);
        var fileDto = new FileDto(
            Id: 17,
            FullFileName: filePath,
            UploadId: 117,
            FolderId: "folder-id"
        );

        apiClientMock
            .SetupSequence(x =>
                x.UploadFileAsync(
                    config,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    "folder-id",
                    fileInfo.Length,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new TimeoutException("UploadG signed URL request timed out"))
            .ReturnsAsync(
                new UploadFileResponse(
                    Status: "success",
                    FileEntry: new FileEntry(19, Path.GetFileName(filePath), null, "text")
                )
            );

        apiClientMock
            .Setup(x =>
                x.GetOrCreateShareableLinkAsync(config, 19, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync("https://uploadg.com/drive/s/hash-value");

        try
        {
            // Act
            var result = await service.UploadFileAsync(fileDto, config, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.FileUrl.ShouldBe("https://uploadg.com/drive/s/hash-value");
            apiClientMock.Verify(
                x =>
                    x.UploadFileAsync(
                        config,
                        It.IsAny<Stream>(),
                        Path.GetFileName(filePath),
                        "folder-id",
                        fileInfo.Length,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Exactly(2)
            );
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyIsInvalid_ReturnsFailure()
    {
        // Arrange
        var config = new UploadGConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.IsApiKeyValidAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Invalid credentials");
    }
}
