using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Hosters.Hitfile;
using Bearcat.Hosters.Shared;
using Bearcat.Hosters.Shared.CostAction.Api;
using Bearcat.Hosters.Shared.CostAction.Api.Models;
using Bearcat.Hosters.Turbobit;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.CostAction;

public class CostActionHosterBaseTest
{
    private const string ApiKey = "costaction-api-key";

    private Mock<ICostActionApiClient> apiClientMock = null!;
    private Hosters.Turbobit.Turbobit turbobit = null!;
    private Hosters.Hitfile.Hitfile hitfile = null!;
    private TurbobitConfig turbobitConfig = null!;
    private HitfileConfig hitfileConfig = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        apiClientMock = new Mock<ICostActionApiClient>(MockBehavior.Strict);

        turbobit = new Hosters.Turbobit.Turbobit(
            apiClientMock.Object,
            new Mock<ILogger<Hosters.Turbobit.Turbobit>>().Object
        )
        {
            UploadRetryDelay = TimeSpan.Zero,
        };

        hitfile = new Hosters.Hitfile.Hitfile(
            apiClientMock.Object,
            new Mock<ILogger<Hosters.Hitfile.Hitfile>>().Object
        )
        {
            UploadRetryDelay = TimeSpan.Zero,
        };

        turbobitConfig = new TurbobitConfig { ApiKey = ApiKey };
        hitfileConfig = new HitfileConfig { ApiKey = ApiKey };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        await File.WriteAllTextAsync(filePath, "costaction test content");
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_Turbobit_UploadsWithTurbobitAppTypeAndReturnsFileIdAsExternalId()
    {
        // Arrange
        var fileDto = new FileDto(Id: 1, FullFileName: filePath, UploadId: 11, FolderId: "15");
        SetupUpload(
            "fd1",
            "15",
            new CostActionUploadResult("cye2wolk2zz7", "https://trbt.cc/cye2wolk2zz7.html")
        );

        // Act
        var result = await turbobit.UploadFileAsync(
            fileDto,
            turbobitConfig,
            NullUploadProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe("https://trbt.cc/cye2wolk2zz7.html");
        result.ExternalId.ShouldBe("cye2wolk2zz7");
    }

    [Test]
    public async Task UploadFileAsync_Hitfile_UploadsWithHitfileAppType()
    {
        // Arrange
        var fileDto = new FileDto(Id: 1, FullFileName: filePath, UploadId: 11);
        SetupUpload("fd2", null, new CostActionUploadResult("tHzfhvR", "https://htfl.net/tHzfhvR"));

        // Act
        var result = await hitfile.UploadFileAsync(
            fileDto,
            hitfileConfig,
            NullUploadProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe("https://htfl.net/tHzfhvR");
    }

    [Test]
    public async Task UploadFileAsync_UploadKeepsFailing_ReturnsAllErrorMessages()
    {
        // Arrange
        var fileDto = new FileDto(Id: 1, FullFileName: filePath, UploadId: 11);
        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    ApiKey,
                    "fd1",
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("Wrong upload url"));

        // Act
        var result = await turbobit.UploadFileAsync(
            fileDto,
            turbobitConfig,
            NullUploadProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessages.ShouldBe(["Wrong upload url", "Wrong upload url", "Wrong upload url"]);
    }

    [Test]
    public async Task CheckFilesExistAsync_MixedUrlsAndExternalIds_MapsStatusBackToUrls()
    {
        // Arrange
        FileUrlToCheckDto[] files =
        [
            new("https://trbt.cc/online1.html", ExternalId: null),
            new("https://turbobit.net/online1/file.rar.html", ExternalId: null),
            new("https://turbobit.net/whatever.html", ExternalId: "offline2"),
        ];

        apiClientMock
            .Setup(x =>
                x.CheckFilesAsync(
                    ApiKey,
                    "fd1",
                    It.Is<IReadOnlyList<string>>(ids =>
                        ids.SequenceEqual(new[] { "online1", "offline2" })
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<string, LinkCheckStatus>
                {
                    ["online1"] = new(IsOnline: true, DownloadCount: 4),
                    ["offline2"] = new(IsOnline: false, DownloadCount: null),
                }
            );

        // Act
        var result = await turbobit.CheckFilesExistAsync(
            turbobitConfig,
            files,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl["https://trbt.cc/online1.html"].ShouldBeTrue();
        result.StatusPerFileUrl["https://turbobit.net/online1/file.rar.html"].ShouldBeTrue();
        result.StatusPerFileUrl["https://turbobit.net/whatever.html"].ShouldBeFalse();
        result.DownloadCountPerFileUrl!["https://trbt.cc/online1.html"].ShouldBe(4);
        result.DownloadCountPerFileUrl.ShouldNotContainKey("https://turbobit.net/whatever.html");
    }

    [Test]
    public async Task CheckFilesExistAsync_ApiFails_ReturnsFailure()
    {
        // Arrange
        apiClientMock
            .Setup(x =>
                x.CheckFilesAsync(
                    ApiKey,
                    "fd2",
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("Too many attempts"));

        // Act
        var result = await hitfile.CheckFilesExistAsync(
            hitfileConfig,
            [new FileUrlToCheckDto("https://htfl.net/tHzfhvR", ExternalId: null)],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessages.ShouldBe(["Too many attempts"]);
    }

    [Test]
    public async Task MoveFileToFolderAsync_WithoutExternalId_UsesIdFromUrl()
    {
        // Arrange
        apiClientMock
            .Setup(x =>
                x.MoveFileToFolderAsync(ApiKey, "tHzfhvR", "2479851", It.IsAny<CancellationToken>())
            )
            .Returns(Task.CompletedTask);

        // Act
        await hitfile.MoveFileToFolderAsync(
            "https://hitfile.net/tHzfhvR/file.rar.html",
            externalId: null,
            "2479851",
            hitfileConfig,
            CancellationToken.None
        );

        // Assert
        apiClientMock.VerifyAll();
    }

    [Test]
    public async Task TryLoginAsync_AccessDenied_ReturnsApiMessage()
    {
        // Arrange
        apiClientMock
            .Setup(x => x.VerifyAccessAsync(ApiKey, "fd2", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("fd2 user does not exist"));

        // Act
        var result = await hitfile.TryLoginAsync(hitfileConfig, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("fd2 user does not exist");
    }

    [Test]
    public void DeserializeHosterConfig_SerializedConfig_RoundTripsApiKey()
    {
        // Arrange
        var serialized = turbobit.SerializeHosterConfig(
            new Dictionary<string, string> { ["ApiKey"] = ApiKey }
        );

        // Act
        var config = turbobit.DeserializeHosterConfig(serialized);

        // Assert
        config.ShouldBe(turbobitConfig);
    }

    [TestCase("https://trbt.cc/cye2wolk2zz7.html", "cye2wolk2zz7")]
    [TestCase("https://turbobit.net/cye2wolk2zz7.html", "cye2wolk2zz7")]
    [TestCase("https://turbobit.net/cye2wolk2zz7/file.part1.rar.html", "cye2wolk2zz7")]
    [TestCase("https://htfl.net/tHzfhvR", "tHzfhvR")]
    [TestCase("https://hitfile.net/tHzfhvR/file.rar.html", "tHzfhvR")]
    [TestCase("https://hitfile.net/", null)]
    [TestCase("not a url", null)]
    public void ExtractFileId_Url_ReturnsCaseSensitiveFileId(string url, string? expected)
    {
        // Act
        var fileId = Hosters.Turbobit.Turbobit.ExtractFileId(url);

        // Assert
        fileId.ShouldBe(expected);
    }

    private void SetupUpload(string appType, string? folderId, CostActionUploadResult result)
    {
        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    ApiKey,
                    appType,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    new FileInfo(filePath).Length,
                    folderId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(result);
    }
}
