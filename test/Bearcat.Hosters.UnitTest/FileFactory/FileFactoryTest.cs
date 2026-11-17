using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Exceptions;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Transfers;
using Bearcat.Hosters.FileFactory;
using Bearcat.Hosters.FileFactory.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.FileFactory;

public class FileFactoryTest
{
    private const string OnlineViewhash = "3b42efa34953f497b27e6a9220ea72c5";

    private const string DeletedViewhash = "3b42efa34953f497b27e6a9220ea72c6";

    private Mock<IFileFactoryApiClient> apiClientMock = null!;
    private Hosters.FileFactory.FileFactory service = null!;
    private FileFactoryConfig config = null!;
    private string filePath = null!;

    [SetUp]
    public async Task SetUp()
    {
        apiClientMock = new Mock<IFileFactoryApiClient>(MockBehavior.Strict);
        service = new Hosters.FileFactory.FileFactory(
            apiClientMock.Object,
            new Mock<ILogger<Hosters.FileFactory.FileFactory>>().Object
        )
        {
            UploadRetryDelay = TimeSpan.Zero,
        };

        config = new FileFactoryConfig
        {
            Email = "bearcat@example.com",
            Password = "filefactory-password",
        };
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
        await File.WriteAllTextAsync(filePath, "filefactory test content");
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
            .ThrowsAsync(new HosterCredentialsRejectedException("Server Error"));

        // Act
        await Should.ThrowAsync<HosterCredentialsRejectedException>(() =>
            service.UploadFileAsync(
                fileDto: new FileDto(Id: 17, FullFileName: filePath, UploadId: 117),
                hosterConfig: config,
                progress: NullTransferProgress.Instance,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert
        apiClientMock.Verify(
            x =>
                x.UploadFileAsync(
                    It.IsAny<FileFactoryConfig>(),
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
    public async Task UploadFileAsync_UploadIntoFolderSucceeds_StoresFileIdAsExternalId()
    {
        // Arrange
        apiClientMock
            .Setup(x =>
                x.UploadFileAsync(
                    config,
                    It.IsAny<Stream>(),
                    Path.GetFileName(filePath),
                    new FileInfo(filePath).Length,
                    "folder-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FileFactoryUploadResult(
                    FileId: "x17bs33m3smp0yvfqsbr3jyjxs8japhr",
                    FileUrl: $"https://www.filefactory.com/file/{OnlineViewhash}"
                )
            );

        // Act
        var result = await service.UploadFileAsync(
            fileDto: new FileDto(
                Id: 17,
                FullFileName: filePath,
                UploadId: 117,
                FolderId: "folder-1"
            ),
            hosterConfig: config,
            progress: NullTransferProgress.Instance,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.FileUrl.ShouldBe($"https://www.filefactory.com/file/{OnlineViewhash}");
        result.ExternalId.ShouldBe("x17bs33m3smp0yvfqsbr3jyjxs8japhr");
    }

    [Test]
    public async Task MoveFilesToFolderAsync_OneFileWithoutExternalId_MovesTheOtherFilesInOneRequest()
    {
        // Arrange
        IReadOnlyList<string>? requestedFileIds = null;

        apiClientMock
            .Setup(x =>
                x.MoveFilesToFolderAsync(
                    config,
                    It.IsAny<IReadOnlyList<string>>(),
                    "folder-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    FileFactoryConfig _,
                    IReadOnlyList<string> fileIds,
                    string _,
                    CancellationToken _
                ) => requestedFileIds = fileIds
            )
            .Returns(Task.CompletedTask);

        // Act
        var movedFileLinks = await service.MoveFilesToFolderAsync(
            files:
            [
                new HosterFileToMoveDto(
                    $"https://www.filefactory.com/file/{OnlineViewhash}",
                    "x17bs33m3smp0yvfqsbr3jyjxs8japhr"
                ),
                new HosterFileToMoveDto(
                    $"https://www.filefactory.com/file/{DeletedViewhash}",
                    ExternalId: null
                ),
                new HosterFileToMoveDto(
                    "https://www.filefactory.com/file/3b42efa34953f497b27e6a9220ea72c7",
                    "x27bs33m3smp0yvfqsbr3jyjxs8japhr"
                ),
            ],
            folderId: "folder-1",
            hosterConfig: config,
            cancellationToken: CancellationToken.None
        );

        // Assert
        movedFileLinks.ShouldBe([
            $"https://www.filefactory.com/file/{OnlineViewhash}",
            "https://www.filefactory.com/file/3b42efa34953f497b27e6a9220ea72c7",
        ]);
        requestedFileIds.ShouldBe([
            "x17bs33m3smp0yvfqsbr3jyjxs8japhr",
            "x27bs33m3smp0yvfqsbr3jyjxs8japhr",
        ]);
    }

    [Test]
    public async Task MoveFilesToFolderAsync_MoveRequestFails_ReturnsNoMovedFileLinks()
    {
        // Arrange
        apiClientMock
            .Setup(x =>
                x.MoveFilesToFolderAsync(
                    config,
                    It.IsAny<IReadOnlyList<string>>(),
                    "unknown-folder",
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("[Request ID: 5f1c2a] Server Error"));

        // Act
        var movedFileLinks = await service.MoveFilesToFolderAsync(
            files:
            [
                new HosterFileToMoveDto(
                    $"https://www.filefactory.com/file/{OnlineViewhash}",
                    "x17bs33m3smp0yvfqsbr3jyjxs8japhr"
                ),
            ],
            folderId: "unknown-folder",
            hosterConfig: config,
            cancellationToken: CancellationToken.None
        );

        // Assert
        movedFileLinks.ShouldBeEmpty();
    }

    [Test]
    public async Task CheckFilesExistAsync_LinksWithTrailingSlashAndFileName_MapsStatusPerUrlAndSkipsForeignLinks()
    {
        // Arrange
        var onlineFileUrl = $"https://filefactory.com/file/{OnlineViewhash}/";
        var offlineFileUrl =
            $"https://www.filefactory.com/file/{DeletedViewhash}/archive.part1.rar";
        var foreignFileUrl = $"https://example.com/file/{DeletedViewhash}";

        apiClientMock
            .Setup(x =>
                x.GetFileInfoPerFileIdAsync(
                    config,
                    It.Is<IReadOnlyList<string>>(fileIds =>
                        fileIds.SequenceEqual(new[] { OnlineViewhash, DeletedViewhash })
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new Dictionary<string, HostedFileInfo>
                {
                    [OnlineViewhash] = new(IsOnline: true, "archive.part1.rar", SizeBytes: null),
                    [DeletedViewhash] = new(IsOnline: false, FileName: null, SizeBytes: null),
                }
            );

        // Act
        var result = await service.CheckFilesExistAsync(
            hosterConfig: config,
            files:
            [
                new FileUrlToCheckDto(onlineFileUrl, ExternalId: null),
                new FileUrlToCheckDto(offlineFileUrl, ExternalId: null),
                new FileUrlToCheckDto(foreignFileUrl, ExternalId: null),
            ],
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.StatusPerFileUrl[onlineFileUrl].ShouldBeTrue();
        result.StatusPerFileUrl[offlineFileUrl].ShouldBeFalse();
        result.StatusPerFileUrl.ShouldNotContainKey(foreignFileUrl);
    }

    [TestCase(
        "https://www.filefactory.com/file/3b42efa34953f497b27e6a9220ea72c5/",
        "https://www.filefactory.com/file/3b42efa34953f497b27e6a9220ea72c5"
    )]
    [TestCase(
        "http://filefactory.com/file/3b42efa34953f497b27e6a9220ea72c5/archive.part1.rar",
        "https://www.filefactory.com/file/3b42efa34953f497b27e6a9220ea72c5"
    )]
    [TestCase("https://www.filefactory.com/file/3b42efa34953f497", null)]
    [TestCase("https://www.filefactory.com/folder/3b42efa34953f497b27e6a9220ea72c5", null)]
    public void ConvertToOwnFileLinkFormat_FileLink_ReturnsNormalizedLinkOrNull(
        string fileLink,
        string? expectedFileLink
    )
    {
        // Act
        var result = service.ConvertToOwnFileLinkFormat(fileLink);

        // Assert
        result.ShouldBe(expectedFileLink);
    }
}
