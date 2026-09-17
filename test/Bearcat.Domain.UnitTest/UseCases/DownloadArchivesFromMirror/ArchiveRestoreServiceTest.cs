using System.Linq.Expressions;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Cancellation;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.DownloadArchivesFromMirror;

public class ArchiveRestoreServiceTest
{
    private FakeArchiveRestoreRepository repository = null!;
    private FakeDownloadHoster downloadHoster = null!;
    private Mock<IFileSystemService> fileSystemServiceMock = null!;
    private Mock<INotificationService> notificationServiceMock = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;
    private RecordingDownloadProgressTracker downloadProgressTracker = null!;
    private DownloadCancellationRegistry cancellationRegistry = null!;
    private string restoreFolderPath = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeArchiveRestoreRepository();
        downloadHoster = new FakeDownloadHoster();
        downloadProgressTracker = new RecordingDownloadProgressTracker();
        cancellationRegistry = new DownloadCancellationRegistry();
        restoreFolderPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(restoreFolderPath);

        fileSystemServiceMock = new Mock<IFileSystemService>();
        fileSystemServiceMock
            .Setup(x => x.CreateTempDirectory(It.IsAny<string>()))
            .Returns(() => restoreFolderPath);

        notificationServiceMock = new Mock<INotificationService>();

        hosterFactoryMock = new Mock<IHosterFactory>();
        hosterFactoryMock.Setup(x => x.GetByName("FakeDownloadHoster")).Returns(downloadHoster);
        hosterFactoryMock
            .Setup(x => x.GetByName("HosterWithoutDownload"))
            .Returns(Mock.Of<IHoster>());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(restoreFolderPath))
        {
            Directory.Delete(restoreFolderPath, recursive: true);
        }
    }

    [Test]
    public async Task ProcessAsync_UploadCarriesOverOnlineFile_DownloadsOnlyTheMissingFiles()
    {
        // Arrange
        var scenario = CreateScenario();
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        downloadHoster.DownloadedLinks.ShouldBe(
            ["https://mirror.test/file-2", "https://mirror.test/file-3"],
            ignoreOrder: true
        );
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Created);
        scenario.Archive.ArchiveFolderPath.ShouldBe(restoreFolderPath);
        scenario.Archive.ArchiveFiles[0].FullFileName.ShouldBe("/gone/archive.part01.rar");
        scenario
            .Archive.ArchiveFiles[1]
            .FullFileName.ShouldBe(Path.Join(restoreFolderPath, "archive.part02.rar"));
        scenario.Archive.ArchiveFiles[1].Md5Hash.ShouldNotBeNull();
    }

    [Test]
    public async Task ProcessAsync_HosterReportsFileSizes_PassesThemToTrackerAndDownload()
    {
        // Arrange
        CreateScenario();
        downloadHoster.SizeBytesPerLink["https://mirror.test/file-2"] = 4711;
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        downloadHoster.ExpectedSizeBytesPerLink["https://mirror.test/file-2"].ShouldBe(4711);
        downloadHoster.ExpectedSizeBytesPerLink["https://mirror.test/file-3"].ShouldBeNull();
        var plannedFiles = downloadProgressTracker.PlannedFilesPerArchiveId[3];
        plannedFiles.Select(file => file.ArchiveFileId).ShouldBe([2, 3]);
        plannedFiles[0].SizeBytes.ShouldBe(4711);
        plannedFiles[1].SizeBytes.ShouldBeNull();
    }

    [Test]
    public async Task ProcessAsync_FileSizeLookupFails_StillRestoresWithUnknownSizes()
    {
        // Arrange
        var scenario = CreateScenario();
        downloadHoster.SizeBytesPerLink["https://mirror.test/file-2"] = 4711;
        downloadHoster.FileSizeLookupException = new HttpRequestException("check_link is down");
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Created);
        downloadProgressTracker
            .PlannedFilesPerArchiveId[3]
            .ShouldAllBe(file => file.SizeBytes == null);
        downloadHoster.ExpectedSizeBytesPerLink.Values.ShouldAllBe(size => size == null);
    }

    [Test]
    public async Task ProcessAsync_HosterAlwaysReuploadsAllFiles_DownloadsEveryArchiveFile()
    {
        // Arrange
        var scenario = CreateScenario();
        scenario.WaitingUpload.UploadConfig.HosterRegistration.AlwaysReuploadAllFiles = true;
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        downloadHoster.DownloadedLinks.Count.ShouldBe(3);
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Created);
    }

    [Test]
    public async Task ProcessAsync_NoHosterEnabledForMirrorDownloads_CreatesNotificationAndKeepsState()
    {
        // Arrange
        var scenario = CreateScenario();
        scenario.DonorUpload.UploadConfig.HosterRegistration.UseForMirrorDownloads = false;
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        downloadHoster.DownloadedLinks.ShouldBeEmpty();
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        scenario.WaitingUpload.UploadState.ShouldBe(UploadState.Failed);
        scenario.WaitingUpload.ErrorMessages.ShouldContain(message =>
            message.Contains("No online mirror")
        );
        VerifyRestoreFailedNotification(Times.Once());
    }

    [Test]
    public async Task ProcessAsync_DownloadedFileHasDifferentHash_ResetsArchiveAndDeletesFolder()
    {
        // Arrange
        var scenario = CreateScenario();
        scenario.DonorUpload.UploadedFiles.Single(f => f.ArchiveFileId == 2).Md5Hash =
            "00000000000000000000000000000000";
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        fileSystemServiceMock.Verify(x => x.DeleteDirectoryIfExists(restoreFolderPath), Times.Once);
        scenario.WaitingUpload.UploadState.ShouldBe(UploadState.Failed);
        scenario.WaitingUpload.ErrorMessages.ShouldContain(message =>
            message.Contains("Archive restore failed")
        );
        VerifyRestoreFailedNotification(Times.Once());
    }

    [Test]
    public async Task ProcessAsync_UserCancelsRunningDownload_DiscardsRestoreAndCancelsWaitingUploads()
    {
        // Arrange
        var scenario = CreateScenario();
        downloadHoster.BlockUntilCanceled = true;
        var service = CreateService();

        // Act
        var processTask = service.ProcessAsync(CancellationToken.None);
        await downloadHoster.DownloadStarted.Task;
        var cancellationRequested = cancellationRegistry.RequestCancellation(scenario.Archive.Id);
        await processTask;

        // Assert
        cancellationRequested.ShouldBeTrue();
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        fileSystemServiceMock.Verify(x => x.DeleteDirectoryIfExists(restoreFolderPath), Times.Once);
        scenario.WaitingUpload.UploadState.ShouldBe(UploadState.Canceled);
        scenario.WaitingUpload.ErrorMessages.ShouldBeEmpty();
        VerifyUploadCanceledNotification(Times.Once());
        VerifyRestoreFailedNotification(Times.Never());
        cancellationRegistry.RequestCancellation(scenario.Archive.Id).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_HosterReportsCancellationAsFailure_StillTakesTheCancelPath()
    {
        // Arrange
        var scenario = CreateScenario();
        downloadHoster.BlockUntilCanceled = true;
        downloadHoster.ReportCancellationAsFailure = true;
        var service = CreateService();

        // Act
        var processTask = service.ProcessAsync(CancellationToken.None);
        await downloadHoster.DownloadStarted.Task;
        cancellationRegistry.RequestCancellation(scenario.Archive.Id);
        await processTask;

        // Assert
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        scenario.WaitingUpload.UploadState.ShouldBe(UploadState.Canceled);
        VerifyUploadCanceledNotification(Times.Once());
        VerifyRestoreFailedNotification(Times.Never());
    }

    [Test]
    public void RequestCancellation_NoRestoreIsRunning_ReturnsFalse()
    {
        // Arrange
        var registry = new DownloadCancellationRegistry();

        // Act
        var result = registry.RequestCancellation(4711);

        // Assert
        result.ShouldBeFalse();
    }

    [Test]
    public void RequestCancellation_ArchiveIsRegistered_CancelsTheTokenOnce()
    {
        // Arrange
        var registry = new DownloadCancellationRegistry();
        var token = registry.Register(3);

        // Act
        var firstResult = registry.RequestCancellation(3);
        registry.Unregister(3);
        var secondResult = registry.RequestCancellation(3);

        // Assert
        firstResult.ShouldBeTrue();
        token.IsCancellationRequested.ShouldBeTrue();
        secondResult.ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_ArchiveStuckInRestoring_DiscardsFolderAndMarksArchiveDeleted()
    {
        // Arrange
        var interruptedArchive = new Archive
        {
            Id = 99,
            ArchiveConfigId = 7,
            ArchiveFolderPath = "/tmp/interrupted-restore",
            ArchiveState = ArchiveState.Restoring,
            ArchiveFiles = [],
            Uploads = [],
        };
        repository.Archives.Add(interruptedArchive);
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        interruptedArchive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        fileSystemServiceMock.Verify(
            x => x.DeleteDirectoryIfExists("/tmp/interrupted-restore"),
            Times.Once
        );
    }

    private ArchiveRestoreService CreateService()
    {
        var configurationProviderMock = new Mock<IApplicationConfigurationProvider>();
        configurationProviderMock
            .Setup(x =>
                x.GetValue<DownloadConfiguration>(
                    It.IsAny<Expression<Func<DownloadConfiguration, int>>>()
                )
            )
            .Returns(2);

        var secretProtectorMock = new Mock<ISecretProtector>();
        secretProtectorMock.Setup(x => x.Unprotect(It.IsAny<string>())).Returns("{}");

        return new ArchiveRestoreService(
            repository,
            hosterFactoryMock.Object,
            fileSystemServiceMock.Object,
            secretProtectorMock.Object,
            notificationServiceMock.Object,
            configurationProviderMock.Object,
            downloadProgressTracker,
            cancellationRegistry,
            NullLogger<ArchiveRestoreService>.Instance
        );
    }

    private void VerifyUploadCanceledNotification(Times times)
    {
        notificationServiceMock.Verify(
            x =>
                x.Create(
                    NotificationKind.UploadCanceled,
                    It.IsAny<string>(),
                    It.IsAny<Upload>(),
                    It.IsAny<Expression<Func<Notification, Upload?>>>()
                ),
            times
        );
    }

    private void VerifyRestoreFailedNotification(Times times)
    {
        notificationServiceMock.Verify(
            x =>
                x.Create(
                    NotificationKind.ArchiveRestoreFailed,
                    It.IsAny<string>(),
                    It.IsAny<Archive>(),
                    It.IsAny<Expression<Func<Notification, Archive?>>>()
                ),
            times
        );
    }

    private Scenario CreateScenario()
    {
        var release = new Release { Id = 1, ReleaseType = ReleaseType.Unmanaged };
        var archiveConfig = new ArchiveConfig
        {
            Id = 7,
            ReleaseId = release.Id,
            Release = release,
            ArchiveFilesBasePath = "/archives",
        };

        var archive = new Archive
        {
            Id = 3,
            ArchiveConfigId = archiveConfig.Id,
            ArchiveConfig = archiveConfig,
            ArchiveFolderPath = "/gone",
            ArchiveState = ArchiveState.Deleted,
            Uploads = [],
            ArchiveFiles =
            [
                new ArchiveFile { Id = 1, FullFileName = "/gone/archive.part01.rar" },
                new ArchiveFile { Id = 2, FullFileName = "/gone/archive.part02.rar" },
                new ArchiveFile { Id = 3, FullFileName = "/gone/archive.part03.rar" },
            ],
        };

        var mirrorRegistration = new HosterRegistration
        {
            Id = 11,
            Name = "Mirror",
            SerializedConfig = "protected",
            HosterClassName = "FakeDownloadHoster",
            IsActive = true,
            UseForMirrorDownloads = true,
        };

        var targetRegistration = new HosterRegistration
        {
            Id = 12,
            Name = "Target",
            SerializedConfig = "protected",
            HosterClassName = "HosterWithoutDownload",
            IsActive = true,
        };

        var mirrorUploadConfig = new UploadConfig
        {
            Id = 21,
            ArchiveConfigId = archiveConfig.Id,
            ArchiveConfig = archiveConfig,
            HosterRegistrationId = mirrorRegistration.Id,
            HosterRegistration = mirrorRegistration,
            Release = release,
        };

        var targetUploadConfig = new UploadConfig
        {
            Id = 22,
            ArchiveConfigId = archiveConfig.Id,
            ArchiveConfig = archiveConfig,
            HosterRegistrationId = targetRegistration.Id,
            HosterRegistration = targetRegistration,
            Release = release,
        };

        var donorUpload = new Upload
        {
            Id = 31,
            ArchiveId = archive.Id,
            UploadConfigId = mirrorUploadConfig.Id,
            UploadConfig = mirrorUploadConfig,
            UploadState = UploadState.Completed,
            UploadedFiles =
            [
                CreateUploadedFile(31, 1, "https://mirror.test/file-1"),
                CreateUploadedFile(31, 2, "https://mirror.test/file-2"),
                CreateUploadedFile(31, 3, "https://mirror.test/file-3"),
            ],
        };

        var previousTargetUpload = new Upload
        {
            Id = 32,
            ArchiveId = archive.Id,
            UploadConfigId = targetUploadConfig.Id,
            UploadConfig = targetUploadConfig,
            UploadState = UploadState.Completed,
            UploadedFiles = [CreateUploadedFile(32, 1, "https://target.test/file-1")],
        };

        var waitingUpload = new Upload
        {
            Id = 33,
            ArchiveId = null,
            UploadConfigId = targetUploadConfig.Id,
            UploadConfig = targetUploadConfig,
            UploadState = UploadState.WaitingForArchive,
            UploadedFiles = [],
        };

        repository.Archives.Add(archive);
        repository.Uploads.AddRange([donorUpload, previousTargetUpload, waitingUpload]);
        repository.SerializedConfigs[mirrorRegistration.Id] = "protected";

        return new Scenario(archive, donorUpload, waitingUpload);
    }

    private sealed class RecordingDownloadProgressTracker : IDownloadProgressTracker
    {
        private readonly DownloadProgressTracker inner = new();

        public Dictionary<
            int,
            IReadOnlyList<PlannedDownloadFile>
        > PlannedFilesPerArchiveId { get; } = new();

        public Dictionary<int, string> HosterNamePerArchiveId { get; } = new();

        public void StartTracking(
            int archiveId,
            string hosterName,
            IReadOnlyList<PlannedDownloadFile> plannedFiles
        )
        {
            PlannedFilesPerArchiveId[archiveId] = plannedFiles;
            HosterNamePerArchiveId[archiveId] = hosterName;
            inner.StartTracking(archiveId, hosterName, plannedFiles);
        }

        public void BeginFile(int archiveId, int archiveFileId, string fileName, long? totalBytes)
        {
            inner.BeginFile(archiveId, archiveFileId, fileName, totalBytes);
        }

        public void AddBytes(int archiveId, int archiveFileId, long bytes)
        {
            inner.AddBytes(archiveId, archiveFileId, bytes);
        }

        public void StopTracking(int archiveId)
        {
            inner.StopTracking(archiveId);
        }

        public DownloadProgressSnapshot? Get(int archiveId)
        {
            return inner.Get(archiveId);
        }
    }

    private static UploadedFile CreateUploadedFile(int uploadId, int archiveFileId, string link)
    {
        return new UploadedFile
        {
            Id = uploadId * 100 + archiveFileId,
            UploadId = uploadId,
            ArchiveFileId = archiveFileId,
            HosterFileLink = link,
            OnlineState = OnlineState.Online,
            CheckedAt = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Local),
        };
    }

    private sealed record Scenario(Archive Archive, Upload DonorUpload, Upload WaitingUpload);
}
