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
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Sources;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.DownloadArchivesFromMirror;

public class ArchiveRestoreServiceTest
{
    private FakeArchiveRestoreRepository repository = null!;
    private FakeDownloadHoster downloadHoster = null!;
    private FakeDownloadHoster secondaryDownloadHoster = null!;
    private Mock<IFileSystemService> fileSystemServiceMock = null!;
    private Mock<INotificationService> notificationServiceMock = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;
    private RecordingDownloadProgressTracker downloadProgressTracker = null!;
    private DownloadCancellationRegistry cancellationRegistry = null!;
    private List<string> restoreFailedMessages = null!;
    private string restoreFolderPath = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeArchiveRestoreRepository();
        downloadHoster = new FakeDownloadHoster();
        secondaryDownloadHoster = new FakeDownloadHoster();
        downloadProgressTracker = new RecordingDownloadProgressTracker();
        cancellationRegistry = new DownloadCancellationRegistry();
        restoreFolderPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(restoreFolderPath);

        fileSystemServiceMock = new Mock<IFileSystemService>();
        fileSystemServiceMock
            .Setup(x => x.CreateTempDirectory(It.IsAny<string>()))
            .Returns(() => restoreFolderPath);

        restoreFailedMessages = [];

        notificationServiceMock = new Mock<INotificationService>();
        notificationServiceMock
            .Setup(x =>
                x.Create(
                    NotificationKind.ArchiveRestoreFailed,
                    It.IsAny<string>(),
                    It.IsAny<Archive>(),
                    It.IsAny<Expression<Func<Notification, Archive?>>>()
                )
            )
            .Callback<NotificationKind, string, Archive, Expression<Func<Notification, Archive?>>>(
                (_, message, _, _) => restoreFailedMessages.Add(message)
            );

        hosterFactoryMock = new Mock<IHosterFactory>();
        hosterFactoryMock.Setup(x => x.GetByName("FakeDownloadHoster")).Returns(downloadHoster);
        hosterFactoryMock
            .Setup(x => x.GetByName("SecondaryFakeDownloadHoster"))
            .Returns(secondaryDownloadHoster);
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
        downloadHoster.DownloadAttemptsPerLink["https://mirror.test/file-2"].ShouldBe(3);
        scenario.WaitingUpload.UploadState.ShouldBe(UploadState.Failed);
        scenario.WaitingUpload.ErrorMessages.ShouldContain(message =>
            message.Contains("Archive restore failed")
        );
        restoreFailedMessages.Single().ShouldContain("MD5 mismatch");
        VerifyRestoreFailedNotification(Times.Once());
    }

    [Test]
    public async Task ProcessAsync_DownloadFailsOnceAndThenSucceeds_RestoresTheArchive()
    {
        // Arrange
        var scenario = CreateScenario();
        downloadHoster.FailedAttemptsBeforeSuccess = 1;
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Created);
        downloadHoster.DownloadAttemptsPerLink["https://mirror.test/file-2"].ShouldBe(2);
        downloadHoster.DownloadAttemptsPerLink["https://mirror.test/file-3"].ShouldBe(2);
        downloadHoster.DownloadedLinks.Count.ShouldBe(4);
        VerifyRestoreFailedNotification(Times.Never());
    }

    [Test]
    public async Task ProcessAsync_DownloadAlwaysFails_RetriesUntilTheAttemptLimitIsReached()
    {
        // Arrange
        var scenario = CreateScenario();
        downloadHoster.AlwaysFails = true;
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        downloadHoster.DownloadAttemptsPerLink["https://mirror.test/file-2"].ShouldBe(3);
        downloadHoster.DownloadAttemptsPerLink["https://mirror.test/file-3"].ShouldBe(3);
        downloadHoster.DownloadedLinks.Count.ShouldBe(6);
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        scenario.WaitingUpload.UploadState.ShouldBe(UploadState.Failed);

        var errorMessage = scenario.WaitingUpload.ErrorMessages.Single();
        errorMessage.ShouldContain("archive.part02.rar");
        errorMessage.ShouldContain("Mirror (3 attempts)");
        errorMessage.ShouldContain("up to 3 attempts per mirror");
        errorMessage.ShouldContain("2 of 2 archive files");
        errorMessage.ShouldContain("InternalServerError");

        restoreFailedMessages.Single().ShouldBe(errorMessage);
    }

    [Test]
    public async Task ProcessAsync_MirrorFileIsGone_FailsWithoutRetrying()
    {
        // Arrange
        var scenario = CreateScenario();
        downloadHoster.ReportsFileAsMissing = true;
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        downloadHoster.DownloadAttemptsPerLink["https://mirror.test/file-2"].ShouldBe(1);
        downloadHoster.DownloadAttemptsPerLink["https://mirror.test/file-3"].ShouldBe(1);
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        restoreFailedMessages.Single().ShouldContain("does not exist on Mirror any more");
        scenario
            .WaitingUpload.ErrorMessages.Single()
            .ShouldContain("does not exist on Mirror any more");
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
    public async Task ProcessAsync_UserCancelsWhileTheFirstHosterFails_DoesNotFallBackToTheSecondHoster()
    {
        // Arrange
        var scenario = CreateScenario();
        downloadHoster.BlockUntilCanceled = true;
        downloadHoster.ReportCancellationAsFailure = true;
        AddSecondaryMirrorUpload(scenario, [2, 3]);
        var service = CreateService();

        // Act
        var processTask = service.ProcessAsync(CancellationToken.None);
        await downloadHoster.DownloadStarted.Task;
        cancellationRegistry.RequestCancellation(scenario.Archive.Id);
        await processTask;

        // Assert
        secondaryDownloadHoster.DownloadedLinks.ShouldBeEmpty();
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        fileSystemServiceMock.Verify(x => x.DeleteDirectoryIfExists(restoreFolderPath), Times.Once);
        scenario.WaitingUpload.UploadState.ShouldBe(UploadState.Canceled);
        scenario.WaitingUpload.ErrorMessages.ShouldBeEmpty();
        VerifyUploadCanceledNotification(Times.Once());
        VerifyRestoreFailedNotification(Times.Never());
    }

    [Test]
    public async Task ProcessAsync_FileIsMissingOnTheFirstHoster_FallsBackToTheSecondHoster()
    {
        // Arrange
        var scenario = CreateScenario();
        downloadHoster.MissingLinks.Add("https://mirror.test/file-2");
        AddSecondaryMirrorUpload(scenario, [2]);
        var missingFile = scenario.DonorUpload.UploadedFiles.Single(file =>
            file.ArchiveFileId == 2
        );
        var checkedAtBefore = missingFile.CheckedAt;
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Created);
        secondaryDownloadHoster.DownloadedLinks.ShouldBe(["https://mirror2.test/file-2"]);
        missingFile.OnlineState.ShouldBe(OnlineState.Offline);
        missingFile.CheckedAt.ShouldNotBe(checkedAtBefore);
        VerifyRestoreFailedNotification(Times.Never());
    }

    [Test]
    public async Task ProcessAsync_FirstHosterExhaustsAllAttempts_FallsBackToTheSecondHoster()
    {
        // Arrange
        var scenario = CreateScenario();
        downloadHoster.AlwaysFails = true;
        AddSecondaryMirrorUpload(scenario, [2, 3]);
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Created);
        downloadHoster.DownloadAttemptsPerLink["https://mirror.test/file-2"].ShouldBe(3);
        downloadHoster.DownloadAttemptsPerLink["https://mirror.test/file-3"].ShouldBe(3);
        secondaryDownloadHoster.DownloadedLinks.ShouldBe(
            ["https://mirror2.test/file-2", "https://mirror2.test/file-3"],
            ignoreOrder: true
        );
        VerifyRestoreFailedNotification(Times.Never());
    }

    [Test]
    public async Task ProcessAsync_NoUploadCoversAllFiles_CombinesTheSourcesOfBothUploads()
    {
        // Arrange
        var scenario = CreateScenario();
        scenario.DonorUpload.UploadedFiles.RemoveAll(file => file.ArchiveFileId == 3);
        AddSecondaryMirrorUpload(scenario, [3]);
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Created);
        downloadHoster.DownloadedLinks.ShouldBe(["https://mirror.test/file-2"]);
        secondaryDownloadHoster.DownloadedLinks.ShouldBe(["https://mirror2.test/file-3"]);
        scenario
            .Archive.ArchiveFiles[2]
            .FullFileName.ShouldBe(Path.Join(restoreFolderPath, "archive.part03.rar"));
        VerifyRestoreFailedNotification(Times.Never());
    }

    [Test]
    public async Task ProcessAsync_AllHostersFail_ReportsEveryHosterInTheFailureChain()
    {
        // Arrange
        var scenario = CreateScenario();
        downloadHoster.AlwaysFails = true;
        secondaryDownloadHoster.AlwaysFails = true;
        AddSecondaryMirrorUpload(scenario, [2, 3]);
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        scenario.WaitingUpload.UploadState.ShouldBe(UploadState.Failed);

        var errorMessage = scenario.WaitingUpload.ErrorMessages.Single();
        errorMessage.ShouldContain("Mirror (3 attempts)");
        errorMessage.ShouldContain("Mirror2 (3 attempts)");
        errorMessage.ShouldContain("2 of 2 archive files");
        restoreFailedMessages.Single().ShouldBe(errorMessage);
    }

    [Test]
    public async Task ProcessAsync_ManagedReleaseAndOneFileHasNoSource_KeepsTheArchiveForRepacking()
    {
        // Arrange
        var scenario = CreateScenario();
        scenario.Archive.ArchiveConfig.Release.ReleaseType = ReleaseType.Managed;
        scenario.DonorUpload.UploadedFiles.RemoveAll(file => file.ArchiveFileId == 3);
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        downloadHoster.DownloadedLinks.ShouldBeEmpty();
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        scenario.Archive.ArchiveFolderPath.ShouldBe("/gone");
        scenario.WaitingUpload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        VerifyRestoreFailedNotification(Times.Never());
    }

    [Test]
    public async Task ProcessAsync_FallsBackToAnotherHoster_TracksThatHosterForTheFile()
    {
        // Arrange
        var scenario = CreateScenario();
        downloadHoster.MissingLinks.Add("https://mirror.test/file-2");
        AddSecondaryMirrorUpload(scenario, [2]);
        var service = CreateService();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        scenario.Archive.ArchiveState.ShouldBe(ArchiveState.Created);
        downloadProgressTracker.BegunFiles.ShouldBe(
            [new BegunFile(2, "Mirror2"), new BegunFile(3, "Mirror")],
            ignoreOrder: true
        );
        downloadProgressTracker
            .PlannedFilesPerArchiveId[3]
            .ShouldAllBe(file => file.HosterName == "Mirror");
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
            .Setup(x => x.GetConfiguration<DownloadConfiguration>())
            .Returns(() =>
                new DownloadConfiguration
                {
                    MaxParallelDownloads = 2,
                    MaxDownloadAttempts = 3,
                    DownloadRetryDelaySeconds = 0,
                }
            );

        var secretProtectorMock = new Mock<ISecretProtector>();
        secretProtectorMock.Setup(x => x.Unprotect(It.IsAny<string>())).Returns("{}");

        return new ArchiveRestoreService(
            repository,
            hosterFactoryMock.Object,
            fileSystemServiceMock.Object,
            secretProtectorMock.Object,
            notificationServiceMock.Object,
            configurationProviderMock.Object,
            cancellationRegistry,
            new MirrorSourceResolver(hosterFactoryMock.Object),
            new MirrorDownloadCoordinator(
                downloadProgressTracker,
                new ArchiveFileDownloader(
                    downloadProgressTracker,
                    NullLogger<ArchiveFileDownloader>.Instance
                )
            ),
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

    private void AddSecondaryMirrorUpload(Scenario scenario, IReadOnlyList<int> archiveFileIds)
    {
        var archiveConfig = scenario.Archive.ArchiveConfig;

        var registration = new HosterRegistration
        {
            Id = 13,
            Name = "Mirror2",
            SerializedConfig = "protected",
            HosterClassName = "SecondaryFakeDownloadHoster",
            IsActive = true,
            UseForMirrorDownloads = true,
            MirrorPriority = 200,
        };

        var uploadConfig = new UploadConfig
        {
            Id = 23,
            ArchiveConfigId = archiveConfig.Id,
            ArchiveConfig = archiveConfig,
            HosterRegistrationId = registration.Id,
            HosterRegistration = registration,
            Release = archiveConfig.Release,
        };

        var upload = new Upload
        {
            Id = 34,
            ArchiveId = scenario.Archive.Id,
            UploadConfigId = uploadConfig.Id,
            UploadConfig = uploadConfig,
            UploadState = UploadState.Completed,
            UploadedFiles = archiveFileIds
                .Select(archiveFileId =>
                    CreateUploadedFile(
                        uploadId: 34,
                        archiveFileId: archiveFileId,
                        link: $"https://mirror2.test/file-{archiveFileId}"
                    )
                )
                .ToList(),
        };

        repository.Uploads.Add(upload);
        repository.SerializedConfigs[registration.Id] = "protected";
    }

    private sealed class RecordingDownloadProgressTracker : IDownloadProgressTracker
    {
        private readonly DownloadProgressTracker inner = new();

        public Dictionary<
            int,
            IReadOnlyList<PlannedDownloadFile>
        > PlannedFilesPerArchiveId { get; } = new();

        public List<BegunFile> BegunFiles { get; } = [];

        public void StartTracking(int archiveId, IReadOnlyList<PlannedDownloadFile> plannedFiles)
        {
            PlannedFilesPerArchiveId[archiveId] = plannedFiles;
            inner.StartTracking(archiveId, plannedFiles);
        }

        public void BeginFile(
            int archiveId,
            int archiveFileId,
            string fileName,
            string hosterName,
            long? totalBytes
        )
        {
            lock (BegunFiles)
            {
                BegunFiles.Add(new BegunFile(archiveFileId, hosterName));
            }

            inner.BeginFile(archiveId, archiveFileId, fileName, hosterName, totalBytes);
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

    private sealed record BegunFile(int ArchiveFileId, string HosterName);

    private sealed record Scenario(Archive Archive, Upload DonorUpload, Upload WaitingUpload);
}
