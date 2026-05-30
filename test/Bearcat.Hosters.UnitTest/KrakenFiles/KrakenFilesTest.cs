using System.Net;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.KrakenFiles;
using Bearcat.Hosters.KrakenFiles.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.KrakenFiles;

public class KrakenFilesTest
{
    private Mock<IKrakenFilesApiClient> apiClientMock = null!;
    private Hosters.KrakenFiles.KrakenFiles service = null!;

    [SetUp]
    public void SetUp()
    {
        apiClientMock = new Mock<IKrakenFilesApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.KrakenFiles.KrakenFiles>>();
        service = new Hosters.KrakenFiles.KrakenFiles(apiClientMock.Object, loggerMock.Object);
        service.UploadRetryDelay = TimeSpan.Zero;
    }

    [Test]
    public async Task UploadFileAsync_FolderId_PassesFolderIdToApiClient()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, "content");
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
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new UploadFileResponse
                {
                    Status = (int)HttpStatusCode.OK,
                    Data = new UploadFileData
                    {
                        Url = "https://krakenfiles.com/view/hash/file.bin",
                    },
                }
            );

        try
        {
            // Act
            var result = await service.UploadFileAsync(fileDto, config, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.FileUrl.ShouldBe("https://krakenfiles.com/view/hash/file.bin");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public async Task CreateFolderAsync_Config_CreatesFolderWithApiClient()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x =>
                x.CreateFolderAsync(config, "release-folder", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync("folder-id");

        // Act
        var result = await service.CreateFolderAsync(
            "release-folder",
            config,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("folder-id");
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyIsValid_ReturnsSuccess()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.IsApiKeyValidAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyIsInvalid_ReturnsFailure()
    {
        // Arrange
        var config = new KrakenFilesConfig { ApiKey = "api-key" };

        apiClientMock
            .Setup(x => x.IsApiKeyValidAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Invalid credentials");
    }
}
