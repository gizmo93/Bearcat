using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class ReleaseFolderRetirementServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string MirrorHosterClassName = "MirrorHoster";

    private string tempRootPath = null!;
    private string releaseFolderPath = null!;
    private string archiveFilesBasePath = null!;
    private Mock<IApplicationConfigurationProvider> configurationMock = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;
    private ReleaseFolderRetirementService service = null!;

    [SetUp]
    public void Setup()
    {
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        releaseFolderPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "release"))
            .FullName;
        archiveFilesBasePath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "archives"))
            .FullName;

        configurationMock = new Mock<IApplicationConfigurationProvider>(MockBehavior.Strict);
        hosterFactoryMock = new Mock<IHosterFactory>(MockBehavior.Strict);
        hosterFactoryMock
            .Setup(f => f.GetByName(MirrorHosterClassName))
            .Returns(Mock.Of<IHosterWithDownload>());

        service = CreateService(new FileSystemService(), workingDirectories: [tempRootPath]);
    }

    [TearDown]
    public void DeleteTempRootPath()
    {
        if (Directory.Exists(tempRootPath))
        {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    [Test]
    public async Task ProcessAsync_AutoConvertDisabled_KeepsReleaseManaged()
    {
        // Arrange
        var release = await AddScenarioAsync();
        SetupConfiguration(autoConvertToUnmanaged: false);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeManagedAsync(release);
    }

    [Test]
    public async Task ProcessAsync_ReleaseIsOverdueAndDeletionIsDisabled_ConvertsAndKeepsReleaseFolder()
    {
        // Arrange
        var release = await AddScenarioAsync();
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Releases.SingleAsync(r => r.Id == release.Id);
        var notification = await DbContext.Notifications.SingleAsync(n =>
            n.ReleaseId == release.Id
        );

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        result.ReleaseFolderPath.ShouldBeNull();
        notification.NotificationKind.ShouldBe(NotificationKind.ReleaseAutoConvertedToUnmanaged);
        notification.Message.ShouldContain(releaseFolderPath);
        notification.Message.ShouldContain("You can now delete the release folder yourself");
        Directory.Exists(releaseFolderPath).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_ArchivesLiveInsideReleaseFolder_NotifiesWithWarningMessage()
    {
        // Arrange
        var release = await AddScenarioAsync(archiveInsideReleaseFolder: true);
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Releases.SingleAsync(r => r.Id == release.Id);
        var notification = await DbContext.Notifications.SingleAsync(n =>
            n.ReleaseId == release.Id
        );

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        notification.Message.ShouldContain(releaseFolderPath);
        notification.Message.ShouldContain("delete only the release data there");
    }

    [Test]
    public async Task ProcessAsync_DeletionEnabledAndLocalArchivesExist_DeletesReleaseFolder()
    {
        // Arrange
        var release = await AddScenarioAsync();
        SetupConfiguration(deleteReleaseFolderOnConversion: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var (result, notification) = await GetReleaseAndNotificationAsync(release);

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        result.ReleaseFolderPath.ShouldBeNull();
        notification.NotificationKind.ShouldBe(NotificationKind.ReleaseAutoConvertedToUnmanaged);
        notification.Message.ShouldContain($"Its release folder {releaseFolderPath} was deleted");
        Directory.Exists(releaseFolderPath).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_DeletionEnabledAndRetentionIsZero_DeletesReleaseFolderOnConversion()
    {
        // Arrange
        var release = await AddScenarioAsync(
            uploadedAt: DateTime.UtcNow.AddMinutes(-1),
            uploadsPostedAt: DateTime.UtcNow.AddMinutes(-1)
        );
        SetupConfiguration(releaseFolderRetentionDays: 0, deleteReleaseFolderOnConversion: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var (result, _) = await GetReleaseAndNotificationAsync(release);

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        Directory.Exists(releaseFolderPath).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_DeletionEnabledAndArchiveOnlyOnMirror_ConvertsAndKeepsReleaseFolder()
    {
        // Arrange
        var release = await AddScenarioAsync(
            archiveState: ArchiveState.Deleted,
            mirrorDownloadsEnabled: true
        );
        SetupConfiguration(deleteReleaseFolderOnConversion: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var (result, notification) = await GetReleaseAndNotificationAsync(release);

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        notification.Message.ShouldContain(
            $"The release folder {releaseFolderPath} was kept because not every archive config has a local archive with all archive files on disk"
        );
        Directory.Exists(releaseFolderPath).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_DeletionEnabledAndLocalArchiveFileIsMissing_ConvertsAndKeepsReleaseFolder()
    {
        // Arrange
        var release = await AddScenarioAsync(
            mirrorDownloadsEnabled: true,
            archiveFileExistsOnDisk: false
        );
        SetupConfiguration(deleteReleaseFolderOnConversion: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var (result, notification) = await GetReleaseAndNotificationAsync(release);

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        notification.Message.ShouldContain("not every archive config has a local archive");
        Directory.Exists(releaseFolderPath).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_DeletionEnabledAndArchivesLiveInsideReleaseFolder_KeepsReleaseFolder()
    {
        // Arrange
        var release = await AddScenarioAsync(archiveInsideReleaseFolder: true);
        SetupConfiguration(deleteReleaseFolderOnConversion: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var (result, notification) = await GetReleaseAndNotificationAsync(release);

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        notification.Message.ShouldContain(
            $"The release folder {releaseFolderPath} was kept because the archives of this release are stored inside it"
        );
        Directory.Exists(releaseFolderPath).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_DeletionEnabledAndReleaseFolderIsWorkingDirectory_KeepsReleaseFolder()
    {
        // Arrange
        service = CreateService(new FileSystemService(), workingDirectories: [releaseFolderPath]);
        var release = await AddScenarioAsync();
        SetupConfiguration(deleteReleaseFolderOnConversion: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var (result, notification) = await GetReleaseAndNotificationAsync(release);

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        notification.Message.ShouldContain(
            $"The release folder {releaseFolderPath} was kept because it is a drive root or contains a working directory"
        );
        Directory.Exists(releaseFolderPath).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_DeletionEnabledAndReleaseFolderContainsWorkingDirectory_KeepsReleaseFolder()
    {
        // Arrange
        var workingDirectory = Directory
            .CreateDirectory(Path.Combine(releaseFolderPath, "working-directory"))
            .FullName;
        service = CreateService(new FileSystemService(), workingDirectories: [workingDirectory]);
        var release = await AddScenarioAsync();
        SetupConfiguration(deleteReleaseFolderOnConversion: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var (result, notification) = await GetReleaseAndNotificationAsync(release);

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        notification.Message.ShouldContain("contains a working directory");
        Directory.Exists(workingDirectory).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_DeletionEnabledAndReleaseFolderIsDriveRoot_KeepsReleaseFolder()
    {
        // Arrange
        var fileSystemServiceMock = new Mock<IFileSystemService>(MockBehavior.Strict);
        fileSystemServiceMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        service = CreateService(fileSystemServiceMock.Object, workingDirectories: [tempRootPath]);
        var release = await AddScenarioAsync();
        var driveRootPath = Path.GetPathRoot(releaseFolderPath)!;
        await DbContext
            .Releases.Where(r => r.Id == release.Id)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(r => r.ReleaseFolderPath, driveRootPath)
            );
        SetupConfiguration(deleteReleaseFolderOnConversion: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var (result, notification) = await GetReleaseAndNotificationAsync(release);

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        notification.Message.ShouldContain(
            $"The release folder {driveRootPath} was kept because it is a drive root"
        );
        fileSystemServiceMock.Verify(
            f => f.DeleteDirectoryIfExists(It.IsAny<string>()),
            Times.Never
        );
    }

    [Test]
    public async Task ProcessAsync_DeletingReleaseFolderFails_PersistsConversionAndNotifies()
    {
        // Arrange
        var fileSystemServiceMock = new Mock<IFileSystemService>(MockBehavior.Strict);
        fileSystemServiceMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        fileSystemServiceMock
            .Setup(f => f.DeleteDirectoryIfExists(releaseFolderPath))
            .Throws(new IOException("The folder is in use."));
        service = CreateService(fileSystemServiceMock.Object, workingDirectories: [tempRootPath]);
        var release = await AddScenarioAsync();
        SetupConfiguration(deleteReleaseFolderOnConversion: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var (result, notification) = await GetReleaseAndNotificationAsync(release);

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        result.ReleaseFolderPath.ShouldBeNull();
        notification.Message.ShouldContain(
            $"Its release folder {releaseFolderPath} could not be deleted, so delete it yourself"
        );
        Directory.Exists(releaseFolderPath).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_UploadsPostedAtIsRecent_KeepsReleaseManaged()
    {
        // Arrange
        var release = await AddScenarioAsync(
            uploadedAt: DateTime.UtcNow.AddDays(-60),
            uploadsPostedAt: DateTime.UtcNow.AddDays(-1)
        );
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeManagedAsync(release);
    }

    [Test]
    public async Task ProcessAsync_UploadsPostedAtIsOverdueButUploadIsRecent_ConvertsRelease()
    {
        // Arrange
        var release = await AddScenarioAsync(
            uploadedAt: DateTime.UtcNow.AddDays(-1),
            uploadsPostedAt: DateTime.UtcNow.AddDays(-60)
        );
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Releases.SingleAsync(r => r.Id == release.Id);

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
    }

    [Test]
    public async Task ProcessAsync_ReleaseWasNeverUploadedOrPosted_KeepsReleaseManaged()
    {
        // Arrange
        var release = await AddScenarioAsync(withCompletedUpload: false);
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeManagedAsync(release);
    }

    [Test]
    public async Task ProcessAsync_ArchiveIsMissingAndNoMirrorExists_KeepsReleaseManaged()
    {
        // Arrange
        var release = await AddScenarioAsync(archiveState: ArchiveState.Deleted);
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeManagedAsync(release);
    }

    [Test]
    public async Task ProcessAsync_ArchiveIsMissingButMirrorExists_ConvertsRelease()
    {
        // Arrange
        var release = await AddScenarioAsync(
            archiveState: ArchiveState.Deleted,
            mirrorDownloadsEnabled: true
        );
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Releases.SingleAsync(r => r.Id == release.Id);

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
    }

    [Test]
    public async Task ProcessAsync_ReleaseHasActiveUpload_KeepsReleaseManaged()
    {
        // Arrange
        var release = await AddScenarioAsync(additionalUploadState: UploadState.Uploading);
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeManagedAsync(release);
    }

    [Test]
    public async Task ProcessAsync_ReleaseIsExcluded_KeepsReleaseManaged()
    {
        // Arrange
        var release = await AddScenarioAsync(excludeFromAutoCleanup: true);
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeManagedAsync(release);
    }

    private ReleaseFolderRetirementService CreateService(
        IFileSystemService fileSystemService,
        string[] workingDirectories
    )
    {
        return new ReleaseFolderRetirementService(
            new ReleaseFolderRetirementRepository(DbContext),
            configurationMock.Object,
            new UnmanagedReleaseConverter(new MirrorCoverageEvaluator(hosterFactoryMock.Object)),
            fileSystemService,
            Options.Create(
                new WorkingDirectoriesConfig { WorkingDirectories = workingDirectories }
            ),
            new NotificationService(
                repository: new NotificationRepository(DbContext),
                timeProvider: CreateTimeProvider(),
                configurationProvider: CreateNotificationConfigurationProvider()
            ),
            CreateTimeProvider(),
            NullLogger<ReleaseFolderRetirementService>.Instance
        );
    }

    private void SetupConfiguration(
        bool autoConvertToUnmanaged = true,
        int releaseFolderRetentionDays = 14,
        bool deleteReleaseFolderOnConversion = false
    )
    {
        configurationMock
            .Setup(c => c.GetValue<ArchiveCleanupConfiguration>(a => a.AutoConvertToUnmanaged))
            .Returns(autoConvertToUnmanaged);
        configurationMock
            .Setup(c => c.GetValue<ArchiveCleanupConfiguration>(a => a.ReleaseFolderRetentionDays))
            .Returns(releaseFolderRetentionDays);
        configurationMock
            .Setup(c =>
                c.GetValue<ArchiveCleanupConfiguration>(a => a.DeleteReleaseFolderOnConversion)
            )
            .Returns(deleteReleaseFolderOnConversion);
    }

    private async Task<(Release Release, Notification Notification)> GetReleaseAndNotificationAsync(
        Release release
    )
    {
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Releases.SingleAsync(r => r.Id == release.Id);
        var notification = await DbContext.Notifications.SingleAsync(n =>
            n.ReleaseId == release.Id
        );

        return (result, notification);
    }

    private async Task ShouldStillBeManagedAsync(Release release)
    {
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Releases.SingleAsync(r => r.Id == release.Id);

        result.ReleaseType.ShouldBe(ReleaseType.Managed);
        result.ReleaseFolderPath.ShouldBe(releaseFolderPath);
        (await DbContext.Notifications.AnyAsync(n => n.ReleaseId == release.Id)).ShouldBeFalse();
    }

    private async Task<Release> AddScenarioAsync(
        DateTime? uploadedAt = null,
        DateTime? uploadsPostedAt = null,
        bool withCompletedUpload = true,
        ArchiveState archiveState = ArchiveState.Created,
        bool mirrorDownloadsEnabled = false,
        bool excludeFromAutoCleanup = false,
        bool archiveInsideReleaseFolder = false,
        bool archiveFileExistsOnDisk = true,
        UploadState? additionalUploadState = null
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var release = new Release
        {
            Name = "Bearcat.Release.001",
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = releaseFolderPath,
            UploadsPostedAt = uploadsPostedAt,
            ExcludeFromAutoCleanup = excludeFromAutoCleanup,
            ReleaseGroup = releaseGroup,
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = archiveFilesBasePath,
            ArchiverName = "zip",
            ArchiveNamePrefix = "bearcat-release",
            ArchivePassword = "secret",
            ArchiveFileSizeMb = 512,
        };
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = new HosterRegistration
            {
                Name = "Mirror",
                SerializedConfig = "{}",
                HosterClassName = MirrorHosterClassName,
                IsActive = true,
                UseForMirrorDownloads = mirrorDownloadsEnabled,
            },
            Name = "Default upload",
        };

        DbContext.UploadConfigs.Add(uploadConfig);
        await DbContext.SaveChangesAsync();

        var archiveFolderPath = archiveInsideReleaseFolder
            ? Directory.CreateDirectory(Path.Combine(releaseFolderPath, "archives")).FullName
            : Directory
                .CreateDirectory(Path.Combine(archiveFilesBasePath, Guid.NewGuid().ToString("N")))
                .FullName;
        var archiveFile = new ArchiveFile
        {
            FullFileName = Path.Combine(archiveFolderPath, "archive.part1.rar"),
        };
        if (archiveFileExistsOnDisk)
        {
            await File.WriteAllTextAsync(archiveFile.FullFileName, "archive");
        }

        var archive = new Archive
        {
            ArchiveConfigId = archiveConfig.Id,
            ArchiveFolderPath = archiveFolderPath,
            ArchiveState = archiveState,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow.AddDays(-90),
            ArchiveFiles = [archiveFile],
            Uploads = [],
            ErrorMessages = [],
        };

        DbContext.Archives.Add(archive);
        await DbContext.SaveChangesAsync();

        if (withCompletedUpload)
        {
            DbContext.Uploads.Add(
                new Upload
                {
                    UploadConfigId = uploadConfig.Id,
                    ArchiveId = archive.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-61),
                    UploadedAt = uploadedAt ?? DateTime.UtcNow.AddDays(-60),
                    UploadState = UploadState.Completed,
                    OnlineState = OnlineState.Online,
                    ErrorMessages = [],
                    UploadedFiles =
                    [
                        new UploadedFile
                        {
                            ArchiveFileId = archiveFile.Id,
                            HosterFileLink = "https://mirror.test/file-1",
                            OnlineState = OnlineState.Online,
                            CreatedAt = DateTime.UtcNow.AddDays(-60),
                            CheckedAt = DateTime.UtcNow.AddDays(-60),
                        },
                    ],
                }
            );
        }

        if (additionalUploadState is not null)
        {
            DbContext.Uploads.Add(
                new Upload
                {
                    UploadConfigId = uploadConfig.Id,
                    CreatedAt = DateTime.UtcNow,
                    UploadState = additionalUploadState.Value,
                    OnlineState = OnlineState.Unknown,
                    ErrorMessages = [],
                    UploadedFiles = [],
                }
            );
        }

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return release;
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
