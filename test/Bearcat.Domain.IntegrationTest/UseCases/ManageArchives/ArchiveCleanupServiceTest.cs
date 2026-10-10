using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives;
using Bearcat.Domain.UseCases.ManageArchives.StorageFolderMoves;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageArchives;

public class ArchiveCleanupServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string MirrorHosterClassName = "MirrorHoster";

    private string releaseFolderPath = null!;
    private string archiveFilesBasePath = null!;
    private string storageFolderPath = null!;
    private string tempRootPath = null!;
    private Mock<IApplicationConfigurationProvider> configurationMock = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;
    private Mock<ITransferProgressTracker> progressTrackerMock = null!;
    private ITransferCancellationRegistry cancellationRegistry = null!;
    private ArchiveCleanupService service = null!;

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
        storageFolderPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "storage"))
            .FullName;

        configurationMock = new Mock<IApplicationConfigurationProvider>(MockBehavior.Strict);
        hosterFactoryMock = new Mock<IHosterFactory>(MockBehavior.Strict);
        hosterFactoryMock
            .Setup(f => f.GetByName(MirrorHosterClassName))
            .Returns(Mock.Of<IHosterWithDownload>());
        progressTrackerMock = new Mock<ITransferProgressTracker>();
        cancellationRegistry = new TransferCancellationRegistry();

        service = CreateService(new FileSystemService());
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
    public async Task ProcessAsync_RetentionActionOff_KeepsArchive()
    {
        // Arrange
        var archive = await AddScenarioAsync();
        await AddStorageFolderAsync();
        SetupConfiguration(ArchiveRetentionAction.Off);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
    }

    [Test]
    public async Task ProcessAsync_ManagedReleaseWithFolderIsOverdue_DeletesArchiveFiles()
    {
        // Arrange
        var archive = await AddScenarioAsync();
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldBeDeletedAsync(archive);
    }

    [Test]
    public async Task ProcessAsync_UnmanagedReleaseWithMirrorIsOverdue_DeletesArchiveFiles()
    {
        // Arrange
        var archive = await AddScenarioAsync(
            releaseType: ReleaseType.Unmanaged,
            mirrorDownloadsEnabled: true
        );
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldBeDeletedAsync(archive);
    }

    [Test]
    public async Task ProcessAsync_UnmanagedReleaseWithoutMirror_KeepsArchive()
    {
        // Arrange
        var archive = await AddScenarioAsync(releaseType: ReleaseType.Unmanaged);
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
    }

    [Test]
    public async Task ProcessAsync_LastUploadIsTooRecent_KeepsArchive()
    {
        // Arrange
        var archive = await AddScenarioAsync(uploadedAt: DateTime.UtcNow.AddDays(-1));
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
    }

    [Test]
    public async Task ProcessAsync_ReuploadResetsTheClock_KeepsArchive()
    {
        // Arrange
        var archive = await AddScenarioAsync();
        await AddUploadAsync(
            archive,
            UploadState.Completed,
            uploadedAt: DateTime.UtcNow.AddDays(-2),
            assignToArchive: false
        );
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
    }

    [Test]
    public async Task ProcessAsync_ArchiveConfigHasActiveUpload_KeepsArchive()
    {
        // Arrange
        var archive = await AddScenarioAsync();
        await AddUploadAsync(
            archive,
            UploadState.WaitingForArchive,
            uploadedAt: null,
            assignToArchive: false
        );
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
    }

    [Test]
    public async Task ProcessAsync_ReleaseIsExcluded_KeepsArchive()
    {
        // Arrange
        var archive = await AddScenarioAsync(excludeFromAutoCleanup: true);
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
    }

    [Test]
    public async Task ProcessAsync_ArchiveFolderContainsForeignFiles_KeepsFolderOnDisk()
    {
        // Arrange
        var archive = await AddScenarioAsync(
            releaseType: ReleaseType.Unmanaged,
            mirrorDownloadsEnabled: true
        );
        var foreignFilePath = Path.Combine(archive.ArchiveFolderPath, "notes.txt");
        await File.WriteAllTextAsync(foreignFilePath, "user data");
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .SingleAsync(a => a.Id == archive.Id);

        result.ArchiveState.ShouldBe(ArchiveState.Deleted);
        result.ArchiveFiles.ShouldAllBe(f => !File.Exists(f.FullFileName));
        File.Exists(foreignFilePath).ShouldBeTrue();
        Directory.Exists(archive.ArchiveFolderPath).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_DeletingAFileFails_KeepsArchiveCreated()
    {
        // Arrange
        var archive = await AddScenarioAsync();
        var fileSystemServiceMock = new Mock<IFileSystemService>(MockBehavior.Strict);
        fileSystemServiceMock
            .Setup(f => f.DeleteFileIfExists(It.IsAny<string>()))
            .Throws(new IOException("Could not delete archive file"));
        service = CreateService(fileSystemServiceMock.Object);
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
        fileSystemServiceMock.VerifyAll();
    }

    [Test]
    public async Task ProcessAsync_DeleteActionAndArchiveIsInStorageFolder_KeepsArchive()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync();
        var archive = await AddScenarioAsync(archiveStorageFolderId: storageFolder.Id);
        SetupConfiguration(ArchiveRetentionAction.Delete);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
    }

    [Test]
    public async Task ProcessAsync_MoveActionAndStorageFolderFits_MovesArchiveIntoStorageFolder()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync();
        var archive = await AddScenarioAsync();
        var archiveFolderName = Path.GetFileName(archive.ArchiveFolderPath);
        var expectedTargetFolderPath = Path.Join(storageFolderPath, archiveFolderName);
        SetupConfiguration(ArchiveRetentionAction.MoveToStorageFolder);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var result = await LoadArchiveAsync(archive);
        result.ArchiveState.ShouldBe(ArchiveState.Created);
        result.ArchiveStorageFolderId.ShouldBe(storageFolder.Id);
        result.ArchiveFolderPath.ShouldBe(expectedTargetFolderPath);
        result
            .ArchiveFiles.Select(f => f.FullFileName)
            .ShouldBe(
                [
                    Path.Join(expectedTargetFolderPath, "archive.part1.rar"),
                    Path.Join(expectedTargetFolderPath, "archive.part2.rar"),
                ],
                ignoreOrder: true
            );
        result.ArchiveFiles.ShouldAllBe(f => File.ReadAllText(f.FullFileName) == "archive-data");
        archive.ArchiveFiles.ShouldAllBe(f => !File.Exists(f.FullFileName));
        Directory.Exists(archive.ArchiveFolderPath).ShouldBeFalse();
        progressTrackerMock.Verify(
            t =>
                t.StartTracking(
                    new TransferIdentifier(TransferType.ArchiveMoveToStorageFolder, archive.Id),
                    It.Is<IReadOnlyList<TransferFile>>(files =>
                        files.Count == 2 && files.All(file => file.SourceName == "NAS")
                    )
                ),
            Times.Once
        );
        progressTrackerMock.Verify(
            t =>
                t.StopTracking(
                    new TransferIdentifier(TransferType.ArchiveMoveToStorageFolder, archive.Id)
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessAsync_MoveActionAndRetentionIsZero_MovesArchiveRightAfterLastUpload()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync();
        var archive = await AddScenarioAsync(uploadedAt: DateTime.UtcNow.AddMinutes(-1));
        SetupConfiguration(ArchiveRetentionAction.MoveToStorageFolder, archiveRetentionDays: 0);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var result = await LoadArchiveAsync(archive);
        result.ArchiveStorageFolderId.ShouldBe(storageFolder.Id);
    }

    [Test]
    public async Task ProcessAsync_MoveActionAndTargetFolderExists_UsesFolderNameWithArchiveId()
    {
        // Arrange
        await AddStorageFolderAsync();
        var archive = await AddScenarioAsync();
        var archiveFolderName = Path.GetFileName(archive.ArchiveFolderPath);
        var existingFilePath = Path.Join(storageFolderPath, archiveFolderName, "other.rar");
        Directory.CreateDirectory(Path.Join(storageFolderPath, archiveFolderName));
        await File.WriteAllTextAsync(existingFilePath, "other");
        SetupConfiguration(ArchiveRetentionAction.MoveToStorageFolder);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var result = await LoadArchiveAsync(archive);
        result.ArchiveFolderPath.ShouldBe(
            Path.Join(storageFolderPath, $"{archiveFolderName}.{archive.Id}")
        );
        result.ArchiveFiles.ShouldAllBe(f => File.Exists(f.FullFileName));
        File.Exists(existingFilePath).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_MoveActionAndCopyFails_DeletesCopiedFilesAndKeepsArchiveLocal()
    {
        // Arrange
        await AddStorageFolderAsync();
        var archive = await AddScenarioAsync();
        var archiveFolderName = Path.GetFileName(archive.ArchiveFolderPath);
        var targetFolderPath = Path.Join(storageFolderPath, $"{archiveFolderName}.{archive.Id}");
        var blockingFilePath = Path.Join(targetFolderPath, "archive.part2.rar");
        Directory.CreateDirectory(Path.Join(storageFolderPath, archiveFolderName));
        Directory.CreateDirectory(targetFolderPath);
        await File.WriteAllTextAsync(blockingFilePath, "foreign");
        SetupConfiguration(ArchiveRetentionAction.MoveToStorageFolder);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
        var result = await LoadArchiveAsync(archive);
        result.ArchiveStorageFolderId.ShouldBeNull();
        result.ArchiveFolderPath.ShouldBe(archive.ArchiveFolderPath);
        File.Exists(Path.Join(targetFolderPath, "archive.part1.rar")).ShouldBeFalse();
        (await File.ReadAllTextAsync(blockingFilePath)).ShouldBe("foreign");
        var notification = await DbContext.Notifications.SingleAsync();
        notification.NotificationKind.ShouldBe(NotificationKind.ArchiveMoveToStorageFolderFailed);
        notification.ArchiveId.ShouldBe(archive.Id);
        progressTrackerMock.Verify(
            t =>
                t.StopTracking(
                    new TransferIdentifier(TransferType.ArchiveMoveToStorageFolder, archive.Id)
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessAsync_MoveActionAndUserCancels_DeletesCopiedFilesWithoutNotification()
    {
        // Arrange
        await AddStorageFolderAsync();
        var archive = await AddScenarioAsync();
        var cancellationRegistryMock = new Mock<ITransferCancellationRegistry>();
        cancellationRegistryMock
            .Setup(r => r.Register(It.IsAny<TransferIdentifier>()))
            .Returns(new CancellationToken(canceled: true));
        cancellationRegistry = cancellationRegistryMock.Object;
        service = CreateService(new FileSystemService());
        SetupConfiguration(ArchiveRetentionAction.MoveToStorageFolder);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
        Directory.EnumerateFileSystemEntries(storageFolderPath).ShouldBeEmpty();
        (await DbContext.Notifications.AnyAsync()).ShouldBeFalse();
        cancellationRegistryMock.Verify(
            r =>
                r.Unregister(
                    new TransferIdentifier(TransferType.ArchiveMoveToStorageFolder, archive.Id)
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessAsync_MoveActionAndUploadStartsDuringCopy_DeletesCopiedFilesWithoutNotification()
    {
        // Arrange
        await AddStorageFolderAsync();
        var archive = await AddScenarioAsync();
        var uploadConfigId = (
            await DbContext.UploadConfigs.SingleAsync(c =>
                c.ArchiveConfigId == archive.ArchiveConfigId
            )
        ).Id;
        progressTrackerMock
            .Setup(t => t.StopTracking(It.IsAny<TransferIdentifier>()))
            .Callback(() =>
            {
                var dbContext = CreateDbContext();
                dbContext.Uploads.Add(
                    new Upload
                    {
                        UploadConfigId = uploadConfigId,
                        CreatedAt = DateTime.UtcNow,
                        UploadState = UploadState.Pending,
                        OnlineState = OnlineState.Online,
                        ErrorMessages = [],
                        UploadedFiles = [],
                    }
                );
                dbContext.SaveChanges();
            });
        SetupConfiguration(ArchiveRetentionAction.MoveToStorageFolder);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
        (await LoadArchiveAsync(archive)).ArchiveStorageFolderId.ShouldBeNull();
        Directory.EnumerateFileSystemEntries(storageFolderPath).ShouldBeEmpty();
        (await DbContext.Notifications.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_MoveActionAndLocalFileChangesDuringCopy_DeletesCopiedFilesWithoutNotification()
    {
        // Arrange
        await AddStorageFolderAsync();
        var archive = await AddScenarioAsync();
        var changedFilePath = archive.ArchiveFiles[0].FullFileName;
        progressTrackerMock
            .Setup(t => t.StopTracking(It.IsAny<TransferIdentifier>()))
            .Callback(() => File.AppendAllText(changedFilePath, "\0\0"));
        SetupConfiguration(ArchiveRetentionAction.MoveToStorageFolder);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
        (await LoadArchiveAsync(archive)).ArchiveStorageFolderId.ShouldBeNull();
        Directory.EnumerateFileSystemEntries(storageFolderPath).ShouldBeEmpty();
        (await DbContext.Notifications.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_MoveActionAndNoStorageFolderFits_CreatesOneNotificationAcrossRuns()
    {
        // Arrange
        await AddStorageFolderAsync(minimumFreeSpaceGb: 1_000_000);
        var archive = await AddScenarioAsync();
        SetupConfiguration(ArchiveRetentionAction.MoveToStorageFolder);

        // Act
        await service.ProcessAsync(CancellationToken.None);
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
        var notification = await DbContext.Notifications.SingleAsync();
        notification.NotificationKind.ShouldBe(NotificationKind.NoArchiveStorageFolderAvailable);
        notification.ArchiveId.ShouldBe(archive.Id);
        progressTrackerMock.Verify(
            t =>
                t.StartTracking(
                    It.IsAny<TransferIdentifier>(),
                    It.IsAny<IReadOnlyList<TransferFile>>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ProcessAsync_MoveActionAndStorageFolderIsInactive_KeepsArchiveLocal()
    {
        // Arrange
        await AddStorageFolderAsync(isActive: false);
        var archive = await AddScenarioAsync();
        SetupConfiguration(ArchiveRetentionAction.MoveToStorageFolder);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldStillBeCreatedAsync(archive);
        (await LoadArchiveAsync(archive)).ArchiveStorageFolderId.ShouldBeNull();
    }

    private async Task<Archive> LoadArchiveAsync(Archive archive)
    {
        DbContext.ChangeTracker.Clear();

        return await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .SingleAsync(a => a.Id == archive.Id);
    }

    private async Task<ArchiveStorageFolder> AddStorageFolderAsync(
        int minimumFreeSpaceGb = 0,
        bool isActive = true
    )
    {
        var storageFolder = new ArchiveStorageFolder
        {
            Name = "NAS",
            Path = storageFolderPath,
            IsActive = isActive,
            MinimumFreeSpaceGb = minimumFreeSpaceGb,
            Priority = 1,
        };

        DbContext.ArchiveStorageFolders.Add(storageFolder);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return storageFolder;
    }

    private void SetupConfiguration(
        ArchiveRetentionAction archiveRetentionAction = ArchiveRetentionAction.Delete,
        int archiveRetentionDays = 30
    )
    {
        configurationMock
            .Setup(c =>
                c.GetValue<ArchiveCleanupConfiguration, ArchiveRetentionAction>(a =>
                    a.ArchiveRetentionAction
                )
            )
            .Returns(archiveRetentionAction);
        configurationMock
            .Setup(c => c.GetValue<ArchiveCleanupConfiguration>(a => a.ArchiveRetentionDays))
            .Returns(archiveRetentionDays);
    }

    private async Task ShouldBeDeletedAsync(Archive archive)
    {
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .SingleAsync(a => a.Id == archive.Id);

        result.ArchiveState.ShouldBe(ArchiveState.Deleted);
        result.ArchiveFiles.ShouldAllBe(f => !File.Exists(f.FullFileName));
        Directory.Exists(archive.ArchiveFolderPath).ShouldBeFalse();
    }

    private async Task ShouldStillBeCreatedAsync(Archive archive)
    {
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .SingleAsync(a => a.Id == archive.Id);

        result.ArchiveState.ShouldBe(ArchiveState.Created);
        result.ArchiveFiles.ShouldAllBe(f => File.Exists(f.FullFileName));
        Directory.Exists(archive.ArchiveFolderPath).ShouldBeTrue();
    }

    private ArchiveCleanupService CreateService(IFileSystemService fileSystemService)
    {
        return new ArchiveCleanupService(
            new ArchiveCleanupRepository(DbContext),
            configurationMock.Object,
            new MirrorCoverageEvaluator(hosterFactoryMock.Object),
            new LocalArchiveDeleter(fileSystemService),
            new ArchiveStorageFolderMoveService(
                new ArchiveCleanupRepository(DbContext),
                fileSystemService,
                new NotificationService(
                    repository: new NotificationRepository(DbContext),
                    timeProvider: CreateTimeProvider(),
                    configurationProvider: CreateNotificationConfigurationProvider()
                ),
                progressTrackerMock.Object,
                cancellationRegistry,
                Mock.Of<ILogger<ArchiveStorageFolderMoveService>>()
            ),
            CreateTimeProvider(),
            Mock.Of<ILogger<ArchiveCleanupService>>()
        );
    }

    private async Task<Archive> AddScenarioAsync(
        ReleaseType releaseType = ReleaseType.Managed,
        DateTime? uploadedAt = null,
        bool mirrorDownloadsEnabled = false,
        bool excludeFromAutoCleanup = false,
        int? archiveStorageFolderId = null
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
            ReleaseType = releaseType,
            ReleaseFolderPath = releaseType is ReleaseType.Managed ? releaseFolderPath : null,
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

        var archiveFolderPath = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, Guid.NewGuid().ToString("N")))
            .FullName;
        var archiveFiles = new List<ArchiveFile>();

        foreach (var fileName in (string[])["archive.part1.rar", "archive.part2.rar"])
        {
            var filePath = Path.Combine(archiveFolderPath, fileName);
            await File.WriteAllTextAsync(filePath, "archive-data");
            archiveFiles.Add(new ArchiveFile { FullFileName = filePath });
        }

        var archive = new Archive
        {
            ArchiveConfigId = archiveConfig.Id,
            ArchiveFolderPath = archiveFolderPath,
            ArchiveStorageFolderId = archiveStorageFolderId,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow.AddDays(-90),
            ArchiveFiles = archiveFiles,
            Uploads = [],
            ErrorMessages = [],
        };

        DbContext.Archives.Add(archive);
        await DbContext.SaveChangesAsync();

        await AddUploadAsync(
            archive: archive,
            uploadState: UploadState.Completed,
            uploadedAt: uploadedAt ?? DateTime.UtcNow.AddDays(-60),
            assignToArchive: true
        );

        return archive;
    }

    private async Task AddUploadAsync(
        Archive archive,
        UploadState uploadState,
        DateTime? uploadedAt,
        bool assignToArchive
    )
    {
        var uploadConfig = await DbContext.UploadConfigs.FirstAsync(c =>
            c.ArchiveConfigId == archive.ArchiveConfigId
        );
        var archiveFiles = await DbContext
            .ArchiveFiles.Where(f => f.ArchiveId == archive.Id)
            .OrderBy(f => f.Id)
            .ToListAsync();

        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            ArchiveId = assignToArchive ? archive.Id : null,
            CreatedAt = DateTime.UtcNow.AddDays(-61),
            UploadedAt = uploadedAt,
            UploadState = uploadState,
            OnlineState = OnlineState.Online,
            ErrorMessages = [],
            UploadedFiles = assignToArchive
                ? archiveFiles
                    .Select(file => new UploadedFile
                    {
                        ArchiveFileId = file.Id,
                        HosterFileLink = $"https://mirror.test/{file.Id}",
                        OnlineState = OnlineState.Online,
                        CreatedAt = DateTime.UtcNow.AddDays(-60),
                        CheckedAt = DateTime.UtcNow.AddDays(-60),
                    })
                    .ToList()
                : [],
        };

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
