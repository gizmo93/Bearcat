using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Fast2Share;
using Bearcat.Hosters.Fast2Share.Api;
using Bearcat.Hosters.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Fast2Share;

public class Fast2ShareTest
{
    private const string Uuid = "9jUMitPVq3AN";

    private Mock<IFast2ShareApiClient> apiClientMock = null!;
    private Hosters.Fast2Share.Fast2Share service = null!;
    private Fast2ShareConfig config = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        apiClientMock = new Mock<IFast2ShareApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<Hosters.Fast2Share.Fast2Share>>();
        service = new Hosters.Fast2Share.Fast2Share(apiClientMock.Object, loggerMock.Object)
        {
            UploadRetryDelay = TimeSpan.Zero,
        };

        config = new Fast2ShareConfig { ApiKey = "f2s_api-key" };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        await File.WriteAllTextAsync(filePath, "fast2share test content");
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_UploadSucceeds_ReturnsShareUrlAndUuidAsExternalId()
    {
        // Arrange
        var fileDto = new FileDto(Id: 17, FullFileName: filePath, UploadId: 117, FolderId: "12");

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    config,
                    filePath,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    new FileInfo(filePath).Length,
                    "12",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Fast2ShareUploadResult(Uuid, $"https://f2s.im/f/{Uuid}", Deduped: false)
            );

        // Act
        var result = await service.UploadFileAsync(
            fileDto,
            config,
            NullTransferProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe($"https://f2s.im/f/{Uuid}");
        result.ExternalId.ShouldBe(Uuid);
    }

    [Test]
    public async Task UploadFileAsync_FileWasDeduped_ReportsFullFileSizeAsTransferred()
    {
        // Arrange
        var progressMock = new Mock<ITransferProgress>();
        var fileDto = new FileDto(Id: 17, FullFileName: filePath, UploadId: 117);

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    config,
                    filePath,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    It.IsAny<long>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Fast2ShareUploadResult(Uuid, $"https://f2s.im/f/{Uuid}", Deduped: true)
            );

        // Act
        var result = await service.UploadFileAsync(
            fileDto,
            config,
            progressMock.Object,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        progressMock.Verify(
            x => x.ReportBytesTransferred(new FileInfo(filePath).Length),
            Times.Once
        );
    }

    [Test]
    public async Task UploadFileAsync_AllAttemptsFail_ReturnsCollectedErrorMessages()
    {
        // Arrange
        var fileDto = new FileDto(Id: 17, FullFileName: filePath, UploadId: 117);

        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    config,
                    filePath,
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("storage unavailable"));

        // Act
        var result = await service.UploadFileAsync(
            fileDto,
            config,
            NullTransferProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.FileUrl.ShouldBeNull();
        result.ErrorMessages.Count.ShouldBe(3);
        result.ErrorMessages.ShouldAllBe(message => message == "storage unavailable");
    }

    [Test]
    public async Task CheckFilesExistAsync_ApiClientReturnsStatuses_MapsStatusAndDownloadCount()
    {
        // Arrange
        var fileUrl = $"https://f2s.im/f/{Uuid}";

        apiClientMock
            .Setup(x =>
                x.CheckLinksAsync(
                    config,
                    It.IsAny<IReadOnlyList<FileUrlToCheckDto>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<string, LinkCheckStatus>
                {
                    [fileUrl] = new(IsOnline: true, DownloadCount: 5),
                }
            );

        // Act
        var result = await service.CheckFilesExistAsync(
            config,
            [new FileUrlToCheckDto(fileUrl, Uuid)],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl[fileUrl].ShouldBeTrue();
        result.DownloadCountPerFileUrl.ShouldNotBeNull();
        result.DownloadCountPerFileUrl[fileUrl].ShouldBe(5);
    }

    [Test]
    public async Task TryLoginAsync_ApiKeyInvalid_ReturnsFailure()
    {
        // Arrange
        apiClientMock
            .Setup(x => x.EnsureApiKeyAcceptedAsync(config, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HosterCredentialsRejectedException("Invalid or revoked API token."));

        // Act
        var result = await service.TryLoginAsync(config, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Invalid or revoked API token.");
    }
}
