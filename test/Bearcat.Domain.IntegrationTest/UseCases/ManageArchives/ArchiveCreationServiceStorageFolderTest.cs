using System.Security.Cryptography;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Transfers;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives;
using Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;
using Bearcat.Domain.UseCases.ManageArchives.StorageFolderLocalWorkingCopies;
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

public class ArchiveCreationServiceStorageFolderTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string ArchiveFolderName = "archive-folder";
    private const string FirstFileName = "release.part1.rar";
    private const string SecondFileName = "release.part2.rar";

    private string tempRootPath = null!;
    private string releaseFolderPath = null!;
    private string archiveFilesBasePath = null!;
    private string storageFolderPath = null!;
    private Mock<IArchiver> archiverMock = null!;
    private Mock<IArchiverFactory> archiverFactoryMock = null!;
    private RecordingTransferProgressTracker progressTracker = null!;
    private TransferCancellationRegistry cancellationRegistry = null!;
    private FileSystemServiceWithCopyCallback fileSystemService = null!;
    private ArchiveCreationService service = null!;

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

        archiverMock = new Mock<IArchiver>(MockBehavior.Strict);
        archiverMock.SetupGet(a => a.Name).Returns("zip");
        archiverMock.SetupGet(a => a.CanChangeHashInPlace).Returns(true);
        archiverFactoryMock = new Mock<IArchiverFactory>(MockBehavior.Strict);
        archiverFactoryMock.Setup(f => f.GetByName("zip")).Returns(archiverMock.Object);

        progressTracker = new RecordingTransferProgressTracker();
        cancellationRegistry = new TransferCancellationRegistry();
        fileSystemService = new FileSystemServiceWithCopyCallback();

        var notificationService = new NotificationService(
            repository: new NotificationRepository(DbContext),
            timeProvider: CreateTimeProvider(),
            configurationProvider: CreateNotificationConfigurationProvider()
        );

        service = new ArchiveCreationService(
            new ArchiveCreationRepository(DbContext),
            Mock.Of<ILogger<ArchiveCreationService>>(),
            archiverFactoryMock.Object,
            fileSystemService,
            CreateTimeProvider(),
            notificationService,
            CreateNotificationConfigurationProvider(),
            new ReleaseFolderEntriesForPackingService(fileSystemService),
            progressTracker,
            cancellationRegistry,
            new FolderSizeProgressReporter(),
            new ArchiveLocalWorkingCopyService(
                new ArchiveCleanupRepository(DbContext),
                fileSystemService,
                notificationService,
                progressTracker,
                cancellationRegistry,
                Mock.Of<ILogger<ArchiveLocalWorkingCopyService>>()
            )
        );
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
    public async Task ProcessAsync_StorageFolderReadsInPlace_ChangesHashInStorageFolderAndUpdatesStorageFolderHash()
    {
        // Arrange
        var scenario = await AddScenarioAsync(useLocalWorkingCopyForReuploads: false);
        var originalSecondLength = new FileInfo(scenario.StorageFilePaths[1]).Length;

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var upload = await DbContext.Uploads.SingleAsync(u => u.Id == scenario.WaitingUploadId);
        var archive = await LoadArchiveAsync(scenario.ArchiveId);
        var archiveFiles = archive.ArchiveFiles.OrderBy(file => file.Id).ToList();
        var changedSecondBytes = await File.ReadAllBytesAsync(scenario.StorageFilePaths[1]);
        var changedSecondHash = Convert.ToHexString(MD5.HashData(changedSecondBytes));

        upload.ArchiveId.ShouldBe(scenario.ArchiveId);
        upload.UploadState.ShouldBe(UploadState.Pending);
        archive.ArchiveStorageFolderId.ShouldBe(scenario.StorageFolderId);
        archive.ArchiveFolderPath.ShouldBe(scenario.StorageArchiveFolderPath);
        archiveFiles.Select(file => file.FullFileName).ShouldBe(scenario.StorageFilePaths);
        ((long)changedSecondBytes.Length).ShouldBe(originalSecondLength + 1);
        archiveFiles[1].Md5Hash.ShouldBe(changedSecondHash);
        archiveFiles[1].Md5HashInStorageFolder.ShouldBe(changedSecondHash);
        archiveFiles[0].Md5Hash.ShouldBe(scenario.OriginalHashes[0]);
        archiveFiles[0].Md5HashInStorageFolder.ShouldBe(scenario.OriginalHashes[0]);
        fileSystemService.CopiedFileNames.ShouldBeEmpty();
        progressTracker.PlannedFilesPerIdentifier.Keys.ShouldBe([
            new TransferIdentifier(TransferType.ArchiveHashChange, scenario.ArchiveId),
        ]);
        Directory.GetFileSystemEntries(archiveFilesBasePath).ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_LocalWorkingCopyEnabled_CopiesOnlyFilesToUploadAndChangesHashOfLocalCopy()
    {
        // Arrange
        var scenario = await AddScenarioAsync(useLocalWorkingCopyForReuploads: true);
        var workingCopyFilePath = Path.Join(
            archiveFilesBasePath,
            ArchiveFolderName,
            SecondFileName
        );
        var originalStorageBytes = await ReadStorageFilesAsync(scenario);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var upload = await DbContext.Uploads.SingleAsync(u => u.Id == scenario.WaitingUploadId);
        var archive = await LoadArchiveAsync(scenario.ArchiveId);
        var archiveFiles = archive.ArchiveFiles.OrderBy(file => file.Id).ToList();
        var workingCopyBytes = await File.ReadAllBytesAsync(workingCopyFilePath);

        upload.ArchiveId.ShouldBe(scenario.ArchiveId);
        upload.UploadState.ShouldBe(UploadState.Pending);
        archive.ArchiveStorageFolderId.ShouldBe(scenario.StorageFolderId);
        archive.ArchiveFolderPath.ShouldBe(scenario.StorageArchiveFolderPath);
        archiveFiles
            .Select(file => file.FullFileName)
            .ShouldBe([scenario.StorageFilePaths[0], workingCopyFilePath]);
        fileSystemService.CopiedFileNames.ShouldBe([SecondFileName]);
        workingCopyBytes.Length.ShouldBe(originalStorageBytes[1].Length + 1);
        archiveFiles[1].Md5Hash.ShouldBe(Convert.ToHexString(MD5.HashData(workingCopyBytes)));
        archiveFiles[1].Md5HashInStorageFolder.ShouldBe(scenario.OriginalHashes[1]);
        archiveFiles[0].Md5Hash.ShouldBe(scenario.OriginalHashes[0]);
        (await ReadStorageFilesAsync(scenario)).ShouldBe(originalStorageBytes);
    }

    [Test]
    public async Task ProcessAsync_LocalWorkingCopyEnabled_TracksCopyProgressPerCopiedFile()
    {
        // Arrange
        var scenario = await AddScenarioAsync(useLocalWorkingCopyForReuploads: true);
        var copyIdentifier = new TransferIdentifier(
            TransferType.ArchiveCopyIntoLocalWorkingCopy,
            scenario.ArchiveId
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var plannedFiles = progressTracker.PlannedFilesPerIdentifier[copyIdentifier];
        plannedFiles
            .Select(file => (file.FileName, file.SourceName))
            .ShouldBe([(SecondFileName, "NAS")]);
        progressTracker.StoppedIdentifiers.ShouldContain(copyIdentifier);
        progressTracker.GetTrackedIds(TransferType.ArchiveCopyIntoLocalWorkingCopy).ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_WorkingCopyFolderNameIsTakenByOtherFiles_CopiesIntoFolderNameWithArchiveId()
    {
        // Arrange
        var scenario = await AddScenarioAsync(useLocalWorkingCopyForReuploads: true);
        var takenFolderPath = Directory
            .CreateDirectory(Path.Join(archiveFilesBasePath, ArchiveFolderName))
            .FullName;
        await File.WriteAllTextAsync(Path.Join(takenFolderPath, SecondFileName), "other");

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var archive = await LoadArchiveAsync(scenario.ArchiveId);
        var expectedFilePath = Path.Join(
            archiveFilesBasePath,
            $"{ArchiveFolderName}.{scenario.ArchiveId}",
            SecondFileName
        );

        archive.ArchiveFolderPath.ShouldBe(scenario.StorageArchiveFolderPath);
        archive
            .ArchiveFiles.OrderBy(file => file.Id)
            .Last()
            .FullFileName.ShouldBe(expectedFilePath);
        File.Exists(expectedFilePath).ShouldBeTrue();
        (await File.ReadAllTextAsync(Path.Join(takenFolderPath, SecondFileName))).ShouldBe("other");
    }

    [Test]
    public async Task ProcessAsync_LocalWorkingCopyEnabled_UploadStaysWaitingUntilCopyFinished()
    {
        // Arrange
        var scenario = await AddScenarioAsync(useLocalWorkingCopyForReuploads: true);
        var uploadStatesDuringCopy = new List<(UploadState UploadState, int? ArchiveId)>();
        fileSystemService.BeforeCopyFileAsync = async _ =>
        {
            await using var readDbContext = Database.CreateDbContext();
            var upload = await readDbContext.Uploads.SingleAsync(u =>
                u.Id == scenario.WaitingUploadId
            );
            uploadStatesDuringCopy.Add((upload.UploadState, upload.ArchiveId));
        };

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        uploadStatesDuringCopy.ShouldBe([(UploadState.WaitingForArchive, null)]);
        DbContext.ChangeTracker.Clear();
        var upload = await DbContext.Uploads.SingleAsync(u => u.Id == scenario.WaitingUploadId);
        upload.UploadState.ShouldBe(UploadState.Pending);
    }

    [Test]
    public async Task ProcessAsync_CopyIntoLocalWorkingCopyFails_RollsBackAndUploadReadsArchiveInPlace()
    {
        // Arrange
        var scenario = await AddScenarioAsync(useLocalWorkingCopyForReuploads: true);
        FailCopyOf(SecondFileName);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var upload = await DbContext.Uploads.SingleAsync(u => u.Id == scenario.WaitingUploadId);
        var archive = await LoadArchiveAsync(scenario.ArchiveId);
        var archiveFiles = archive.ArchiveFiles.OrderBy(file => file.Id).ToList();
        var notification = await DbContext.Notifications.SingleAsync(n =>
            n.NotificationKind == NotificationKind.ArchiveCopyIntoLocalWorkingCopyFailed
        );
        var changedSecondHash = Convert.ToHexString(
            MD5.HashData(await File.ReadAllBytesAsync(scenario.StorageFilePaths[1]))
        );

        upload.ArchiveId.ShouldBe(scenario.ArchiveId);
        upload.UploadState.ShouldBe(UploadState.Pending);
        archive.ArchiveStorageFolderId.ShouldBe(scenario.StorageFolderId);
        archiveFiles.Select(file => file.FullFileName).ShouldBe(scenario.StorageFilePaths);
        archiveFiles[1].Md5Hash.ShouldBe(changedSecondHash);
        archiveFiles[1].Md5HashInStorageFolder.ShouldBe(changedSecondHash);
        Directory.GetFileSystemEntries(archiveFilesBasePath).ShouldBeEmpty();
        notification.ArchiveId.ShouldBe(scenario.ArchiveId);
        notification.Message.ShouldContain("Copy failed");
        progressTracker.GetTrackedIds(TransferType.ArchiveCopyIntoLocalWorkingCopy).ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_CopyFailsAgainWhileNotificationIsUnresolved_DoesNotRepeatNotification()
    {
        // Arrange
        var scenario = await AddScenarioAsync(useLocalWorkingCopyForReuploads: true);
        FailCopyOf(SecondFileName);
        await service.ProcessAsync(CancellationToken.None);
        await CompleteUploadAsync(scenario.WaitingUploadId);
        var secondUploadId = await AddWaitingUploadAsync(scenario.UploadConfigId);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var secondUpload = await DbContext.Uploads.SingleAsync(u => u.Id == secondUploadId);
        secondUpload.ArchiveId.ShouldBe(scenario.ArchiveId);
        secondUpload.UploadState.ShouldBe(UploadState.Pending);
        (
            await DbContext.Notifications.CountAsync(n =>
                n.NotificationKind == NotificationKind.ArchiveCopyIntoLocalWorkingCopyFailed
            )
        ).ShouldBe(1);
        fileSystemService.CopiedFileNames.ShouldBe([SecondFileName, SecondFileName]);
    }

    [Test]
    public async Task ProcessAsync_UserCancelsCopy_RollsBackWithoutNotificationAndUploadReadsArchiveInPlace()
    {
        // Arrange
        var scenario = await AddScenarioAsync(useLocalWorkingCopyForReuploads: true);
        fileSystemService.BeforeCopyFileAsync = _ =>
        {
            cancellationRegistry
                .RequestCancellation(
                    new TransferIdentifier(
                        TransferType.ArchiveCopyIntoLocalWorkingCopy,
                        scenario.ArchiveId
                    )
                )
                .ShouldBeTrue();

            return Task.CompletedTask;
        };

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var upload = await DbContext.Uploads.SingleAsync(u => u.Id == scenario.WaitingUploadId);
        var archive = await LoadArchiveAsync(scenario.ArchiveId);

        upload.ArchiveId.ShouldBe(scenario.ArchiveId);
        upload.UploadState.ShouldBe(UploadState.Pending);
        archive
            .ArchiveFiles.OrderBy(file => file.Id)
            .Select(file => file.FullFileName)
            .ShouldBe(scenario.StorageFilePaths);
        Directory.GetFileSystemEntries(archiveFilesBasePath).ShouldBeEmpty();
        (await DbContext.Notifications.AnyAsync()).ShouldBeFalse();
        cancellationRegistry
            .RequestCancellation(
                new TransferIdentifier(
                    TransferType.ArchiveCopyIntoLocalWorkingCopy,
                    scenario.ArchiveId
                )
            )
            .ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_ArchiveFilesBasePathDoesNotExist_DoesNotCreateItAndUploadReadsArchiveInPlace()
    {
        // Arrange
        var scenario = await AddScenarioAsync(useLocalWorkingCopyForReuploads: true);
        Directory.Delete(archiveFilesBasePath);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var upload = await DbContext.Uploads.SingleAsync(u => u.Id == scenario.WaitingUploadId);
        var archive = await LoadArchiveAsync(scenario.ArchiveId);
        var notification = await DbContext.Notifications.SingleAsync();

        Directory.Exists(archiveFilesBasePath).ShouldBeFalse();
        upload.UploadState.ShouldBe(UploadState.Pending);
        archive
            .ArchiveFiles.OrderBy(file => file.Id)
            .Select(file => file.FullFileName)
            .ShouldBe(scenario.StorageFilePaths);
        notification.NotificationKind.ShouldBe(
            NotificationKind.ArchiveCopyIntoLocalWorkingCopyFailed
        );
        notification.Message.ShouldContain(archiveFilesBasePath);
        fileSystemService.CopiedFileNames.ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_SecondReuploadBeforeCleanup_ReusesExistingWorkingCopyFile()
    {
        // Arrange
        var scenario = await AddScenarioAsync(useLocalWorkingCopyForReuploads: true);
        var workingCopyFilePath = Path.Join(
            archiveFilesBasePath,
            ArchiveFolderName,
            SecondFileName
        );
        await service.ProcessAsync(CancellationToken.None);
        await CompleteUploadAsync(scenario.WaitingUploadId);
        var firstWorkingCopyLength = new FileInfo(workingCopyFilePath).Length;
        var originalStorageBytes = await ReadStorageFilesAsync(scenario);
        var secondUploadId = await AddWaitingUploadAsync(scenario.UploadConfigId);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var secondUpload = await DbContext.Uploads.SingleAsync(u => u.Id == secondUploadId);
        var archive = await LoadArchiveAsync(scenario.ArchiveId);
        var secondArchiveFile = archive.ArchiveFiles.OrderBy(file => file.Id).Last();
        var workingCopyBytes = await File.ReadAllBytesAsync(workingCopyFilePath);

        secondUpload.UploadState.ShouldBe(UploadState.Pending);
        fileSystemService.CopiedFileNames.ShouldBe([SecondFileName]);
        secondArchiveFile.FullFileName.ShouldBe(workingCopyFilePath);
        ((long)workingCopyBytes.Length).ShouldBe(firstWorkingCopyLength + 1);
        secondArchiveFile.Md5Hash.ShouldBe(Convert.ToHexString(MD5.HashData(workingCopyBytes)));
        secondArchiveFile.Md5HashInStorageFolder.ShouldBe(scenario.OriginalHashes[1]);
        (await ReadStorageFilesAsync(scenario)).ShouldBe(originalStorageBytes);
    }

    [Test]
    public async Task ProcessAsync_LocalWorkingCopyEnabledAndFileNotToUploadHasNoHash_LeavesStorageFileUnchanged()
    {
        // Arrange
        var scenario = await AddScenarioAsync(
            useLocalWorkingCopyForReuploads: true,
            firstFileHasHash: false
        );
        var originalStorageBytes = await ReadStorageFilesAsync(scenario);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var archive = await LoadArchiveAsync(scenario.ArchiveId);
        var firstArchiveFile = archive.ArchiveFiles.OrderBy(file => file.Id).First();

        firstArchiveFile.FullFileName.ShouldBe(scenario.StorageFilePaths[0]);
        firstArchiveFile.Md5Hash.ShouldBeNull();
        (await ReadStorageFilesAsync(scenario)).ShouldBe(originalStorageBytes);
    }

    [Test]
    public async Task ProcessAsync_ArchiveFileMissingInStorageFolder_MarksArchiveMissingFilesWithoutCopy()
    {
        // Arrange
        var scenario = await AddScenarioAsync(useLocalWorkingCopyForReuploads: true);
        File.Delete(scenario.StorageFilePaths[1]);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var upload = await DbContext.Uploads.SingleAsync(u => u.Id == scenario.WaitingUploadId);
        var archive = await LoadArchiveAsync(scenario.ArchiveId);

        archive.ArchiveState.ShouldBe(ArchiveState.MissingFiles);
        upload.ArchiveId.ShouldBeNull();
        upload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        fileSystemService.CopiedFileNames.ShouldBeEmpty();
        Directory.GetFileSystemEntries(archiveFilesBasePath).ShouldBeEmpty();
        (
            await DbContext.Notifications.AnyAsync(n =>
                n.NotificationKind == NotificationKind.ArchiveCopyIntoLocalWorkingCopyFailed
            )
        ).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_StorageFolderIsUnreachable_MarksArchiveMissingFilesWithoutCreatingStorageFolder()
    {
        // Arrange
        var scenario = await AddScenarioAsync(useLocalWorkingCopyForReuploads: false);
        Directory.Delete(storageFolderPath, recursive: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var upload = await DbContext.Uploads.SingleAsync(u => u.Id == scenario.WaitingUploadId);
        var archive = await LoadArchiveAsync(scenario.ArchiveId);

        archive.ArchiveState.ShouldBe(ArchiveState.MissingFiles);
        upload.UploadState.ShouldBe(UploadState.WaitingForArchive);
        Directory.Exists(storageFolderPath).ShouldBeFalse();
        Directory.GetFileSystemEntries(archiveFilesBasePath).ShouldBeEmpty();
    }

    private static async Task<List<byte[]>> ReadStorageFilesAsync(StorageFolderScenario scenario)
    {
        var storageBytes = new List<byte[]>();

        foreach (var storageFilePath in scenario.StorageFilePaths)
        {
            storageBytes.Add(await File.ReadAllBytesAsync(storageFilePath));
        }

        return storageBytes;
    }

    private void FailCopyOf(string fileName)
    {
        fileSystemService.BeforeCopyFileAsync = destinationFilePath =>
            Path.GetFileName(destinationFilePath) == fileName
                ? throw new IOException("Copy failed")
                : Task.CompletedTask;
    }

    private async Task<Archive> LoadArchiveAsync(int archiveId)
    {
        return await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .SingleAsync(a => a.Id == archiveId);
    }

    private async Task CompleteUploadAsync(int uploadId)
    {
        DbContext.ChangeTracker.Clear();
        var upload = await DbContext.Uploads.SingleAsync(u => u.Id == uploadId);
        upload.UploadState = UploadState.Completed;
        upload.UploadedAt = DateTime.UtcNow;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    private async Task<int> AddWaitingUploadAsync(int uploadConfigId)
    {
        var upload = new Upload
        {
            UploadConfigId = uploadConfigId,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.WaitingForArchive,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
        };
        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return upload.Id;
    }

    private async Task<StorageFolderScenario> AddScenarioAsync(
        bool useLocalWorkingCopyForReuploads,
        bool firstFileHasHash = true
    )
    {
        var storageArchiveFolderPath = Directory
            .CreateDirectory(Path.Join(storageFolderPath, ArchiveFolderName))
            .FullName;
        var storageFilePaths = new List<string>
        {
            Path.Join(storageArchiveFolderPath, FirstFileName),
            Path.Join(storageArchiveFolderPath, SecondFileName),
        };
        await File.WriteAllTextAsync(storageFilePaths[0], "first-volume");
        await File.WriteAllTextAsync(storageFilePaths[1], "second-volume");
        var originalHashes = storageFilePaths
            .Select(filePath => Convert.ToHexString(MD5.HashData(File.ReadAllBytes(filePath))))
            .ToList();

        var archiveConfig = new ArchiveConfig
        {
            Release = new Release
            {
                Name = "Bearcat.Release.001",
                ReleaseType = ReleaseType.Managed,
                ReleaseFolderPath = releaseFolderPath,
                ReleaseGroup = new ReleaseGroup
                {
                    Name = "Managed releases",
                    EnableAutomaticReuploads = false,
                    NumberOfHoursUntilReupload = 24,
                },
            },
            Name = "Main archive",
            ArchiveFilesBasePath = archiveFilesBasePath,
            ArchiverName = "zip",
            ArchiveNamePrefix = "release",
            ArchiveFileSizeMb = 512,
        };
        var uploadConfig = new UploadConfig
        {
            Release = archiveConfig.Release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = new HosterRegistration
            {
                Name = "TestHoster",
                SerializedConfig = "{}",
                HosterClassName = "TestHoster",
                IsActive = true,
            },
            Name = "Default upload",
        };
        var storageFolder = new ArchiveStorageFolder
        {
            Name = "NAS",
            Path = storageFolderPath,
            IsActive = true,
            UseLocalWorkingCopyForReuploads = useLocalWorkingCopyForReuploads,
        };
        var archiveFiles = storageFilePaths
            .Select(
                (filePath, index) =>
                    new ArchiveFile
                    {
                        FullFileName = filePath,
                        Md5Hash = index == 0 && !firstFileHasHash ? null : originalHashes[index],
                        Md5HashInStorageFolder =
                            index == 0 && !firstFileHasHash ? null : originalHashes[index],
                    }
            )
            .ToList();
        var archive = new Archive
        {
            ArchiveConfig = archiveConfig,
            ArchiveFolderPath = storageArchiveFolderPath,
            ArchiveStorageFolder = storageFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            ArchiveFiles = archiveFiles,
            Uploads = [],
            ErrorMessages = [],
        };
        var previousUpload = new Upload
        {
            UploadConfig = uploadConfig,
            Archive = archive,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UploadedAt = DateTime.UtcNow.AddDays(-30),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            ErrorMessages = [],
            UploadedFiles =
            [
                new UploadedFile
                {
                    ArchiveFile = archiveFiles[0],
                    HosterFileLink = "https://hoster.example/first",
                    OnlineState = OnlineState.Online,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                },
            ],
        };
        var waitingUpload = new Upload
        {
            UploadConfig = uploadConfig,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.WaitingForArchive,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
        };

        DbContext.AddRange(archive, previousUpload, waitingUpload);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return new StorageFolderScenario(
            ArchiveId: archive.Id,
            StorageFolderId: storageFolder.Id,
            UploadConfigId: uploadConfig.Id,
            WaitingUploadId: waitingUpload.Id,
            StorageArchiveFolderPath: storageArchiveFolderPath,
            StorageFilePaths: storageFilePaths,
            OriginalHashes: originalHashes
        );
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }

    private sealed record StorageFolderScenario(
        int ArchiveId,
        int StorageFolderId,
        int UploadConfigId,
        int WaitingUploadId,
        string StorageArchiveFolderPath,
        List<string> StorageFilePaths,
        List<string> OriginalHashes
    );

    private sealed class FileSystemServiceWithCopyCallback : IFileSystemService
    {
        private readonly FileSystemService inner = new();

        public Func<string, Task>? BeforeCopyFileAsync { get; set; }

        public List<string> CopiedFileNames { get; } = [];

        public List<string> GetFoldersInPath(string path) => inner.GetFoldersInPath(path);

        public List<string> GetSelectableFoldersInPath(string path) =>
            inner.GetSelectableFoldersInPath(path);

        public List<string> GetFilesInPath(string path, bool recursive) =>
            inner.GetFilesInPath(path, recursive);

        public FolderFileCountAndSize GetFolderFileCountAndSize(string path) =>
            inner.GetFolderFileCountAndSize(path);

        public string CreateTempDirectory(string basePath) => inner.CreateTempDirectory(basePath);

        public bool FileExists(string filePath) => inner.FileExists(filePath);

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);

        public bool DirectoryHasEntries(string path) => inner.DirectoryHasEntries(path);

        public long? GetAvailableFreeSpaceBytes(string path) =>
            inner.GetAvailableFreeSpaceBytes(path);

        public long GetFileSizeBytes(string filePath) => inner.GetFileSizeBytes(filePath);

        public void CreateDirectory(string path) => inner.CreateDirectory(path);

        public void CopyFile(string sourceFilePath, string destinationFilePath) =>
            inner.CopyFile(sourceFilePath, destinationFilePath);

        public async Task CopyFileAsync(
            string sourceFilePath,
            string destinationFilePath,
            ITransferProgress progress,
            CancellationToken cancellationToken
        )
        {
            CopiedFileNames.Add(Path.GetFileName(destinationFilePath));

            if (BeforeCopyFileAsync is not null)
            {
                await BeforeCopyFileAsync(destinationFilePath);
            }

            await inner.CopyFileAsync(
                sourceFilePath,
                destinationFilePath,
                progress,
                cancellationToken
            );
        }

        public void CopyDirectoryRecursively(
            string sourceDirectoryPath,
            string destinationDirectoryPath
        ) => inner.CopyDirectoryRecursively(sourceDirectoryPath, destinationDirectoryPath);

        public void DeleteFileIfExists(string filePath) => inner.DeleteFileIfExists(filePath);

        public void DeleteDirectoryIfExists(string path) => inner.DeleteDirectoryIfExists(path);

        public void DeleteDirectoryIfEmpty(string path) => inner.DeleteDirectoryIfEmpty(path);

        public IReadOnlyList<string> DeleteDirectoriesByNameRecursively(
            string rootPath,
            string directoryName
        ) => inner.DeleteDirectoriesByNameRecursively(rootPath, directoryName);
    }
}
