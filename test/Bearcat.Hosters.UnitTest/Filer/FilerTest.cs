using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.Filer;
using Bearcat.Hosters.Filer.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Filer;

public class FilerTest
{
    private const string FileHash = "aa0ea9176b7b5b95";

    private Mock<IFilerApiClient> apiClientMock = null!;
    private Hosters.Filer.Filer service = null!;
    private FilerConfig config = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        apiClientMock = new Mock<IFilerApiClient>(MockBehavior.Strict);
        service = new Hosters.Filer.Filer(
            apiClientMock.Object,
            new Mock<ILogger<Hosters.Filer.Filer>>().Object
        )
        {
            UploadRetryDelay = TimeSpan.Zero,
        };

        config = new FilerConfig { Email = "bearcat@example.com", Password = "filer-password" };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        await File.WriteAllTextAsync(filePath, "filer test content");
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(filePath);
    }

    [Test]
    public async Task UploadFileAsync_CredentialsRejected_DoesNotRetry()
    {
        // Arrange
        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    config,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    It.IsAny<long>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HosterCredentialsRejectedException("INVALID_CREDENTIALS"));

        // Act
        await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            service.UploadFileAsync(
                new FileDto(Id: 17, FullFileName: filePath, UploadId: 117),
                config,
                NullTransferProgress.Instance,
                CancellationToken.None
            )
        );

        // Assert
        apiClientMock.Verify(
            x =>
                x.UploadFileAsync(
                    It.IsAny<FilerConfig>(),
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task UploadFileAsync_UploadIntoFolderSucceeds_StoresFileHashAndEditCodeAsExternalId()
    {
        // Arrange
        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    config,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    new FileInfo(filePath).Length,
                    "bf939f014bcd87b4",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FilerUploadResult(
                    FileHash: FileHash,
                    EditCode: "d8f65339142f346a",
                    FileUrl: $"https://filer.net/get/{FileHash}"
                )
            );

        // Act
        var result = await service.UploadFileAsync(
            new FileDto(
                Id: 17,
                FullFileName: filePath,
                UploadId: 117,
                FolderId: "bf939f014bcd87b4"
            ),
            config,
            NullTransferProgress.Instance,
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe($"https://filer.net/get/{FileHash}");
        result.ExternalId.ShouldBe($"{FileHash}:d8f65339142f346a");
    }

    [Test]
    public async Task MoveFileToFolderAsync_ExternalIdWithoutEditCode_ThrowsWithoutCallingApi()
    {
        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            service.MoveFileToFolderAsync(
                fileUrl: $"https://filer.net/get/{FileHash}",
                externalId: null,
                folderId: "bf939f014bcd87b4",
                hosterConfig: config,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        exception.Message.ShouldContain("edit code is unknown");
    }

    [Test]
    public async Task MoveFilesToFolderAsync_ElevenFilesAndSecondRequestFails_ReturnsTheTenFilesOfTheFirstRequest()
    {
        // Arrange
        const string folderHash = "bf939f014bcd87b4";
        var files = Enumerable
            .Range(1, 11)
            .Select(index => new HosterFileToMoveDto(
                $"https://filer.net/get/{index:D16}",
                $"{index:D16}:d8f65339142f346a"
            ))
            .ToList();

        apiClientMock
            .Setup(x =>
                x.MoveFilesToFolderAsync(
                    config,
                    It.Is<IReadOnlyDictionary<string, string>>(editCodes => editCodes.Count == 10),
                    folderHash,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        apiClientMock
            .Setup(x =>
                x.MoveFilesToFolderAsync(
                    config,
                    It.Is<IReadOnlyDictionary<string, string>>(editCodes => editCodes.Count == 1),
                    folderHash,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("Invalid edit code"));

        // Act
        var movedFileLinks = await service.MoveFilesToFolderAsync(
            files: files,
            folderId: folderHash,
            hosterConfig: config,
            cancellationToken: CancellationToken.None
        );

        // Assert
        movedFileLinks.ShouldBe(files.Take(10).Select(file => file.HosterFileLink).ToList());
    }

    [Test]
    public async Task MoveFilesToFolderAsync_OneFileWithoutEditCode_MovesTheOtherFilesInOneRequest()
    {
        // Arrange
        const string folderHash = "bf939f014bcd87b4";
        const string secondFileHash = "36293ad21cebb03b";
        IReadOnlyDictionary<string, string>? requestedEditCodePerFileHash = null;

        apiClientMock
            .Setup(x =>
                x.MoveFilesToFolderAsync(
                    config,
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    folderHash,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    FilerConfig _,
                    IReadOnlyDictionary<string, string> editCodePerFileHash,
                    string _,
                    CancellationToken _
                ) => requestedEditCodePerFileHash = editCodePerFileHash
            )
            .Returns(Task.CompletedTask);

        // Act
        var movedFileLinks = await service.MoveFilesToFolderAsync(
            files:
            [
                new HosterFileToMoveDto(
                    $"https://filer.net/get/{FileHash}",
                    $"{FileHash}:d8f65339142f346a"
                ),
                new HosterFileToMoveDto("https://filer.net/get/1111111111111111", null),
                new HosterFileToMoveDto(
                    $"https://filer.net/get/{secondFileHash}",
                    $"{secondFileHash}:8c717a573f227b24"
                ),
            ],
            folderId: folderHash,
            hosterConfig: config,
            cancellationToken: CancellationToken.None
        );

        // Assert
        movedFileLinks.ShouldBe([
            $"https://filer.net/get/{FileHash}",
            $"https://filer.net/get/{secondFileHash}",
        ]);

        requestedEditCodePerFileHash.ShouldNotBeNull();
        requestedEditCodePerFileHash[FileHash].ShouldBe("d8f65339142f346a");
        requestedEditCodePerFileHash[secondFileHash].ShouldBe("8c717a573f227b24");
        requestedEditCodePerFileHash.Count.ShouldBe(2);
    }

    [Test]
    public async Task CheckFilesExistAsync_LinksWithAndWithoutFileName_MapsStatusPerUrlAndSkipsForeignLinks()
    {
        // Arrange
        var onlineFileUrl = $"https://filer.net/get/{FileHash}/archive.part1.rar";
        var offlineFileUrl = "https://filer.net/get/36293ad21cebb03b";
        var foreignFileUrl = "https://example.com/get/36293ad21cebb03b";

        apiClientMock
            .Setup(x =>
                x.GetFileInfoPerFileHashAsync(
                    It.Is<IReadOnlyList<string>>(hashes =>
                        hashes.SequenceEqual(new[] { FileHash, "36293ad21cebb03b" })
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<string, HostedFileInfo>
                {
                    [FileHash] = new(IsOnline: true, "archive.part1.rar", 1048576),
                    ["36293ad21cebb03b"] = new(IsOnline: false, null, null),
                }
            );

        // Act
        var result = await service.CheckFilesExistAsync(
            config,
            [
                new FileUrlToCheckDto(onlineFileUrl, ExternalId: FileHash),
                new FileUrlToCheckDto(offlineFileUrl, ExternalId: null),
                new FileUrlToCheckDto(foreignFileUrl, ExternalId: null),
            ],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl[onlineFileUrl].ShouldBeTrue();
        result.StatusPerFileUrl[offlineFileUrl].ShouldBeFalse();
        result.StatusPerFileUrl.ShouldNotContainKey(foreignFileUrl);
    }

    [Test]
    public async Task CheckFilesExistAsync_StatusRequestThrows_ReturnsFailedResult()
    {
        // Arrange
        apiClientMock
            .Setup(x =>
                x.GetFileInfoPerFileHashAsync(
                    It.IsAny<IReadOnlyList<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("Filer multi status request failed"));

        // Act
        var result = await service.CheckFilesExistAsync(
            config,
            [new FileUrlToCheckDto($"https://filer.net/get/{FileHash}", ExternalId: null)],
            CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.StatusPerFileUrl.ShouldBeEmpty();
        result.ErrorMessages.ShouldContain("Filer multi status request failed");
    }

    [Test]
    public void SerializeHosterConfig_EmailAndPasswordGiven_RoundTripsThroughDeserialization()
    {
        // Arrange
        var serialized = service.SerializeHosterConfig(
            new Dictionary<string, string>
            {
                [nameof(FilerConfig.Email)] = "bearcat@example.com",
                [nameof(FilerConfig.Password)] = "filer-password",
            }
        );

        // Act
        var result = service.DeserializeHosterConfig(serialized);

        // Assert
        result.ShouldBe(config);
    }
}
