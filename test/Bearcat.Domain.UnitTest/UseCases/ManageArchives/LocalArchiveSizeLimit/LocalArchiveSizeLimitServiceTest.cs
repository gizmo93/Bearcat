using System.Linq.Expressions;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.UseCases.ManageArchives.LocalArchiveSizeLimit;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageArchives.StorageFolderMoves;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageArchives.LocalArchiveSizeLimit;

public class LocalArchiveSizeLimitServiceTest
{
    private const long Gigabyte = ArchiveStorageFolderSelector.BytesPerGigabyte;

    private Mock<IArchiveCleanupRepository> repositoryMock = null!;
    private Mock<IApplicationConfigurationProvider> configurationMock = null!;
    private Mock<IFileSystemService> fileSystemServiceMock = null!;
    private Mock<INotificationService> notificationServiceMock = null!;
    private LocalArchiveSizeLimitService service = null!;

    [SetUp]
    public void Setup()
    {
        repositoryMock = new Mock<IArchiveCleanupRepository>();
        configurationMock = new Mock<IApplicationConfigurationProvider>();
        fileSystemServiceMock = new Mock<IFileSystemService>();
        fileSystemServiceMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        fileSystemServiceMock.Setup(f => f.GetFileSizeBytes(It.IsAny<string>())).Returns(Gigabyte);
        notificationServiceMock = new Mock<INotificationService>();

        service = new LocalArchiveSizeLimitService(
            repositoryMock.Object,
            configurationMock.Object,
            fileSystemServiceMock.Object,
            new MirrorCoverageEvaluator(Mock.Of<IHosterFactory>()),
            new LocalArchiveDeleter(fileSystemServiceMock.Object),
            notificationServiceMock.Object,
            Mock.Of<ILogger<LocalArchiveSizeLimitService>>()
        );
    }

    [TestCase(0, ArchiveRetentionAction.Delete)]
    [TestCase(0, ArchiveRetentionAction.Off)]
    [TestCase(10, ArchiveRetentionAction.MoveToStorageFolder)]
    public async Task DeleteOldestArchivesToFitAsync_LimitOffOrMoveToStorageFolder_DoesNothing(
        int maximumLocalArchiveSizeGb,
        ArchiveRetentionAction retentionAction
    )
    {
        // Arrange
        SetupConfiguration(maximumLocalArchiveSizeGb, retentionAction);

        // Act
        await service.DeleteOldestArchivesToFitAsync(100 * Gigabyte, CancellationToken.None);

        // Assert
        repositoryMock.VerifyNoOtherCalls();
        notificationServiceMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task DeleteOldestArchivesToFitAsync_OverLimit_DeletesOldestRecoverableArchivesUntilNewArchiveFits()
    {
        // Arrange
        SetupConfiguration(maximumLocalArchiveSizeGb: 5, ArchiveRetentionAction.Off);
        SetupLocalArchiveFileCount(5);
        var notRecoverableArchive = CreateArchive(1, recoverable: false);
        var oldestRecoverableArchive = CreateArchive(2, recoverable: true);
        var secondOldestRecoverableArchive = CreateArchive(3, recoverable: true);
        var newestRecoverableArchive = CreateArchive(4, recoverable: true);
        SetupDeletableArchives(
            notRecoverableArchive,
            oldestRecoverableArchive,
            secondOldestRecoverableArchive,
            newestRecoverableArchive
        );

        // Act
        await service.DeleteOldestArchivesToFitAsync(2 * Gigabyte, CancellationToken.None);

        // Assert
        notRecoverableArchive.ArchiveState.ShouldBe(ArchiveState.Created);
        oldestRecoverableArchive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        secondOldestRecoverableArchive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        newestRecoverableArchive.ArchiveState.ShouldBe(ArchiveState.Created);
        notificationServiceMock.VerifyNoOtherCalls();
        repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteOldestArchivesToFitAsync_StillOverLimitAfterDeletingEverything_DeletesArchiveAndCreatesNotification()
    {
        // Arrange
        SetupConfiguration(maximumLocalArchiveSizeGb: 1, ArchiveRetentionAction.Delete);
        SetupLocalArchiveFileCount(3);
        var archive = CreateArchive(1, recoverable: true);
        SetupDeletableArchives(archive);

        // Act
        await service.DeleteOldestArchivesToFitAsync(Gigabyte, CancellationToken.None);

        // Assert
        archive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        notificationServiceMock.Verify(
            n => n.Create(NotificationKind.LocalArchiveSizeLimitExceeded, It.IsAny<string>()),
            Times.Once
        );
        repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteOldestArchivesToFitAsync_StillOverLimitWithUnresolvedNotification_CreatesNoFurtherNotification()
    {
        // Arrange
        SetupConfiguration(maximumLocalArchiveSizeGb: 1, ArchiveRetentionAction.Delete);
        SetupLocalArchiveFileCount(3);
        SetupDeletableArchives();
        repositoryMock
            .Setup(r =>
                r.HasUnresolvedNotificationAsync(
                    NotificationKind.LocalArchiveSizeLimitExceeded,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);

        // Act
        await service.DeleteOldestArchivesToFitAsync(Gigabyte, CancellationToken.None);

        // Assert
        notificationServiceMock.VerifyNoOtherCalls();
    }

    private void SetupConfiguration(
        int maximumLocalArchiveSizeGb,
        ArchiveRetentionAction retentionAction
    )
    {
        configurationMock
            .Setup(c =>
                c.GetValue<ArchiveCleanupConfiguration>(
                    It.IsAny<Expression<Func<ArchiveCleanupConfiguration, int>>>()
                )
            )
            .Returns(maximumLocalArchiveSizeGb);
        configurationMock
            .Setup(c =>
                c.GetValue<ArchiveCleanupConfiguration, ArchiveRetentionAction>(
                    It.IsAny<
                        Expression<Func<ArchiveCleanupConfiguration, ArchiveRetentionAction>>
                    >()
                )
            )
            .Returns(retentionAction);
    }

    private void SetupLocalArchiveFileCount(int fileCount)
    {
        repositoryMock
            .Setup(r => r.GetLocalCreatedArchiveFilePathsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Enumerable
                    .Range(1, fileCount)
                    .Select(index => $"/archives/file-{index}.rar")
                    .ToList()
            );
    }

    private void SetupDeletableArchives(params Archive[] archives)
    {
        repositoryMock
            .Setup(r =>
                r.GetDeletableLocalArchivesOrderedByLastUploadOfArchiveConfigAsync(
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(archives);
    }

    private static Archive CreateArchive(int id, bool recoverable)
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
                Release = new Release
                {
                    Name = $"Release.{id}",
                    ReleaseType = recoverable ? ReleaseType.Managed : ReleaseType.Unmanaged,
                    ReleaseFolderPath = recoverable ? $"/releases/Release.{id}" : null,
                },
                UploadConfigs = [],
            },
            ArchiveFiles = [new ArchiveFile { FullFileName = $"{archiveFolderPath}/archive.rar" }],
            Uploads = [],
            ErrorMessages = [],
        };
    }
}
