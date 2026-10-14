using Bearcat.Abstractions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ArchiveFileCopies;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageArchives.ArchiveFileCopies;

public class ArchiveFileCopyServiceTest
{
    private const string TargetFolderPath = "/mnt/storage/archive-7";

    private static readonly TransferIdentifier TransferIdentifier = new(
        TransferType.ArchiveMoveToStorageFolder,
        7
    );

    private Mock<IFileSystemService> fileSystemServiceMock = null!;
    private Mock<ITransferProgressTracker> progressTrackerMock = null!;
    private ArchiveFileCopyService service = null!;

    [SetUp]
    public void Setup()
    {
        fileSystemServiceMock = new Mock<IFileSystemService>();
        progressTrackerMock = new Mock<ITransferProgressTracker>();

        service = new ArchiveFileCopyService(
            fileSystemServiceMock.Object,
            progressTrackerMock.Object,
            Mock.Of<ILogger<ArchiveFileCopyService>>()
        );
    }

    [Test]
    public async Task CopyArchiveFilesAndVerifySizesAsync_SecondFileHasWrongSize_DeletesCopiedFilesAndStopsTracking()
    {
        // Arrange
        var firstTargetFilePath = Path.Join(TargetFolderPath, "archive.part1.rar");
        var secondTargetFilePath = Path.Join(TargetFolderPath, "archive.part2.rar");
        fileSystemServiceMock.Setup(f => f.GetFileSizeBytes(firstTargetFilePath)).Returns(100);
        fileSystemServiceMock.Setup(f => f.GetFileSizeBytes(secondTargetFilePath)).Returns(50);
        var archiveFilesWithSize = CreateArchiveFilesWithSize();

        // Act
        var exception = await Should.ThrowAsync<IOException>(() =>
            CopyArchiveFilesAsync(archiveFilesWithSize)
        );

        // Assert
        exception.Message.ShouldBe(
            $"Copied file {secondTargetFilePath} has 50 bytes instead of 100 bytes"
        );
        fileSystemServiceMock.Verify(f => f.DeleteFileIfExists(firstTargetFilePath), Times.Once);
        fileSystemServiceMock.Verify(f => f.DeleteDirectoryIfEmpty(TargetFolderPath), Times.Once);
        progressTrackerMock.Verify(t => t.StopTracking(TransferIdentifier), Times.Once);
    }

    [Test]
    public async Task CopyArchiveFilesAndVerifySizesAsync_Success_ReturnsCopiedFilePathPerArchiveFile()
    {
        // Arrange
        fileSystemServiceMock.Setup(f => f.GetFileSizeBytes(It.IsAny<string>())).Returns(100);
        var archiveFilesWithSize = CreateArchiveFilesWithSize();

        // Act
        var copiedFiles = await CopyArchiveFilesAsync(archiveFilesWithSize);

        // Assert
        copiedFiles.ShouldBe([
            new CopiedArchiveFile(
                archiveFilesWithSize[0].ArchiveFile,
                Path.Join(TargetFolderPath, "archive.part1.rar")
            ),
            new CopiedArchiveFile(
                archiveFilesWithSize[1].ArchiveFile,
                Path.Join(TargetFolderPath, "archive.part2.rar")
            ),
        ]);
    }

    private Task<IReadOnlyList<CopiedArchiveFile>> CopyArchiveFilesAsync(
        IReadOnlyList<ArchiveFileWithSize> archiveFilesWithSize
    )
    {
        return service.CopyArchiveFilesAndVerifySizesAsync(
            archiveId: 7,
            archiveFilesWithSize: archiveFilesWithSize,
            targetFolderPath: TargetFolderPath,
            sourceName: "Folder 1",
            transferIdentifier: TransferIdentifier,
            cancellationToken: CancellationToken.None
        );
    }

    private static List<ArchiveFileWithSize> CreateArchiveFilesWithSize()
    {
        return
        [
            new ArchiveFileWithSize(
                new ArchiveFile { Id = 70, FullFileName = "/archives/archive-7/archive.part1.rar" },
                100
            ),
            new ArchiveFileWithSize(
                new ArchiveFile { Id = 71, FullFileName = "/archives/archive-7/archive.part2.rar" },
                100
            ),
        ];
    }
}
