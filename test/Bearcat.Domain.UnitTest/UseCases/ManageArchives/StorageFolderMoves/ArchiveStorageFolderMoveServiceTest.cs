using Bearcat.Abstractions;
using Bearcat.Abstractions.Transfers;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageArchives.StorageFolderMoves;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageArchives.StorageFolderMoves;

public class ArchiveStorageFolderMoveServiceTest
{
    private const long Gigabyte = ArchiveStorageFolderSelector.BytesPerGigabyte;

    private Mock<IArchiveCleanupRepository> repositoryMock = null!;
    private Mock<IFileSystemService> fileSystemServiceMock = null!;
    private Mock<ITransferProgressTracker> progressTrackerMock = null!;
    private ArchiveStorageFolderMoveService service = null!;

    [SetUp]
    public void Setup()
    {
        repositoryMock = new Mock<IArchiveCleanupRepository>();
        repositoryMock
            .Setup(r =>
                r.GetArchiveIdsWithUnresolvedNotificationAsync(
                    It.IsAny<NotificationKind>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);
        fileSystemServiceMock = new Mock<IFileSystemService>();
        fileSystemServiceMock.Setup(f => f.GetFileSizeBytes(It.IsAny<string>())).Returns(Gigabyte);
        fileSystemServiceMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        progressTrackerMock = new Mock<ITransferProgressTracker>();

        service = new ArchiveStorageFolderMoveService(
            repositoryMock.Object,
            fileSystemServiceMock.Object,
            Mock.Of<INotificationService>(),
            progressTrackerMock.Object,
            new TransferCancellationRegistry(),
            Mock.Of<ILogger<ArchiveStorageFolderMoveService>>()
        );
    }

    [Test]
    public async Task MoveArchivesAsync_SeveralArchives_ReadsFreeSpaceAgainBeforeEachArchive()
    {
        // Arrange
        var firstFolder = CreateStorageFolder(1, "/mnt/first");
        var secondFolder = CreateStorageFolder(2, "/mnt/second");
        SetupStorageFolders(firstFolder, secondFolder);
        fileSystemServiceMock
            .SetupSequence(f => f.GetAvailableFreeSpaceBytes(firstFolder.Path))
            .Returns(10 * Gigabyte)
            .Returns(2 * Gigabyte);
        fileSystemServiceMock
            .SetupSequence(f => f.GetAvailableFreeSpaceBytes(secondFolder.Path))
            .Returns(5 * Gigabyte)
            .Returns(5 * Gigabyte);
        var firstArchive = CreateArchive(1);
        var secondArchive = CreateArchive(2);

        // Act
        await service.MoveArchivesAsync([firstArchive, secondArchive], CancellationToken.None);

        // Assert
        firstArchive.ArchiveStorageFolderId.ShouldBe(firstFolder.Id);
        firstArchive.ArchiveFolderPath.ShouldBe(Path.Join(firstFolder.Path, "archive-1"));
        secondArchive.ArchiveStorageFolderId.ShouldBe(secondFolder.Id);
        secondArchive.ArchiveFolderPath.ShouldBe(Path.Join(secondFolder.Path, "archive-2"));
        fileSystemServiceMock.Verify(
            f => f.GetAvailableFreeSpaceBytes(firstFolder.Path),
            Times.Exactly(2)
        );
    }

    [Test]
    public async Task MoveArchivesAsync_Success_StartsAndStopsProgressTracking()
    {
        // Arrange
        var storageFolder = CreateStorageFolder(1, "/mnt/first");
        SetupStorageFolders(storageFolder);
        fileSystemServiceMock
            .Setup(f => f.GetAvailableFreeSpaceBytes(storageFolder.Path))
            .Returns(10 * Gigabyte);
        var archive = CreateArchive(7);
        var transferIdentifier = new TransferIdentifier(
            TransferType.ArchiveMoveToStorageFolder,
            archive.Id
        );

        // Act
        await service.MoveArchivesAsync([archive], CancellationToken.None);

        // Assert
        progressTrackerMock.Verify(
            t =>
                t.StartTracking(
                    transferIdentifier,
                    It.Is<IReadOnlyList<TransferFile>>(files =>
                        files.Single().SizeBytes == Gigabyte
                        && files.Single().SourceName == storageFolder.Name
                    )
                ),
            Times.Once
        );
        progressTrackerMock.Verify(t => t.StopTracking(transferIdentifier), Times.Once);
        repositoryMock.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [Test]
    public async Task MoveArchivesAsync_CopyFails_StopsProgressTrackingAndKeepsArchiveLocal()
    {
        // Arrange
        var storageFolder = CreateStorageFolder(1, "/mnt/first");
        SetupStorageFolders(storageFolder);
        fileSystemServiceMock
            .Setup(f => f.GetAvailableFreeSpaceBytes(storageFolder.Path))
            .Returns(10 * Gigabyte);
        fileSystemServiceMock
            .Setup(f =>
                f.CopyFileAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ITransferProgress>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new IOException("Disk full"));
        var archive = CreateArchive(7);
        var transferIdentifier = new TransferIdentifier(
            TransferType.ArchiveMoveToStorageFolder,
            archive.Id
        );

        // Act
        await service.MoveArchivesAsync([archive], CancellationToken.None);

        // Assert
        archive.ArchiveStorageFolderId.ShouldBeNull();
        archive.ArchiveFolderPath.ShouldBe("/archives/archive-7");
        progressTrackerMock.Verify(
            t => t.StartTracking(transferIdentifier, It.IsAny<IReadOnlyList<TransferFile>>()),
            Times.Once
        );
        progressTrackerMock.Verify(t => t.StopTracking(transferIdentifier), Times.Once);
        fileSystemServiceMock.Verify(
            f => f.DeleteDirectoryIfEmpty(Path.Join(storageFolder.Path, "archive-7")),
            Times.Once
        );
        fileSystemServiceMock.Verify(
            f => f.DeleteFileIfExists("/archives/archive-7/archive.part1.rar"),
            Times.Never
        );
    }

    private void SetupStorageFolders(params ArchiveStorageFolder[] storageFolders)
    {
        repositoryMock
            .Setup(r => r.GetActiveArchiveStorageFoldersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(storageFolders);

        foreach (var storageFolder in storageFolders)
        {
            fileSystemServiceMock.Setup(f => f.DirectoryExists(storageFolder.Path)).Returns(true);
        }
    }

    private static ArchiveStorageFolder CreateStorageFolder(int id, string path)
    {
        return new ArchiveStorageFolder
        {
            Id = id,
            Name = $"Folder {id}",
            Path = path,
            IsActive = true,
            MinimumFreeSpaceGb = 1,
            Priority = 1,
        };
    }

    private static Archive CreateArchive(int id)
    {
        var archiveFolderPath = $"/archives/archive-{id}";

        return new Archive
        {
            Id = id,
            ArchiveFolderPath = archiveFolderPath,
            ArchiveState = ArchiveState.Created,
            ArchiveConfig = new ArchiveConfig
            {
                Name = "Main archive",
                Release = new Release { Name = $"Release.{id}" },
            },
            ArchiveFiles =
            [
                new ArchiveFile
                {
                    Id = id * 10,
                    FullFileName = $"{archiveFolderPath}/archive.part1.rar",
                },
            ],
            Uploads = [],
            ErrorMessages = [],
        };
    }
}
