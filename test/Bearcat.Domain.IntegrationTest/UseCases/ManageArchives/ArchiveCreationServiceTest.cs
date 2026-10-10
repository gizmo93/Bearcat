using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives;
using Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;
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

public class ArchiveCreationServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string PreviouslyComputedMd5Hash = "0123456789ABCDEF0123456789ABCDEF";

    private string releaseFolderPath = null!;
    private string archiveFilesBasePath = null!;
    private string tempRootPath = null!;
    private Mock<IArchiver> archiverMock = null!;
    private Mock<IArchiverFactory> archiverFactoryMock = null!;
    private Mock<IApplicationConfigurationProvider> configurationProviderMock = null!;
    private string additionalContentSourcePath = null!;
    private RecordingTransferProgressTracker progressTracker = null!;
    private TransferCancellationRegistry cancellationRegistry = null!;
    private long releaseFolderBytesWhilePacking;
    private int? userCanceledArchiveId;
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
        additionalContentSourcePath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "additional-content"))
            .FullName;

        archiverMock = new Mock<IArchiver>(MockBehavior.Strict);
        archiverMock.SetupGet(a => a.Name).Returns("zip");
        archiverMock.SetupGet(a => a.FileExtension).Returns(".zip");
        archiverMock.SetupGet(a => a.CanChangeHashInPlace).Returns(true);

        archiverFactoryMock = new Mock<IArchiverFactory>(MockBehavior.Strict);
        archiverFactoryMock.Setup(f => f.GetByName("zip")).Returns(archiverMock.Object);
        configurationProviderMock = new Mock<IApplicationConfigurationProvider>(
            MockBehavior.Strict
        );
        configurationProviderMock
            .Setup(p =>
                p.GetValue<ArchiveRepackagingConfiguration>(
                    It.IsAny<Expression<Func<ArchiveRepackagingConfiguration, string?>>>()
                )
            )
            .Returns(ArchiveRepackagingStrategies.IncrementArchiveFileSize);
        configurationProviderMock
            .Setup(p =>
                p.GetValue<ArchiveRepackagingConfiguration>(
                    It.IsAny<Expression<Func<ArchiveRepackagingConfiguration, int>>>()
                )
            )
            .Returns(2);

        progressTracker = new RecordingTransferProgressTracker();
        cancellationRegistry = new TransferCancellationRegistry();
        service = CreateService(new FileSystemService());
    }

    private ArchiveCreationService CreateService(IFileSystemService fileSystemService)
    {
        return new ArchiveCreationService(
            new ArchiveCreationRepository(DbContext),
            Mock.Of<ILogger<ArchiveCreationService>>(),
            archiverFactoryMock.Object,
            fileSystemService,
            CreateTimeProvider(),
            new NotificationService(
                repository: new NotificationRepository(DbContext),
                timeProvider: CreateTimeProvider(),
                configurationProvider: CreateNotificationConfigurationProvider()
            ),
            configurationProviderMock.Object,
            new ReleaseFolderEntriesForPackingService(fileSystemService),
            progressTracker,
            cancellationRegistry,
            new FolderSizeProgressReporter()
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
    public async Task ProcessAsync_CreatingArchiveExists_DeletesOrphanedArchive()
    {
        // Arrange
        var archiveConfig = await AddArchiveConfigAsync();
        var orphanedArchive = new Archive
        {
            ArchiveConfigId = archiveConfig.Id,
            ArchiveFolderPath = Path.Combine(archiveFilesBasePath, "orphaned"),
            ArchiveState = ArchiveState.Creating,
            ArchiveFileSizeMb = archiveConfig.ArchiveFileSizeMb,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [],
            Uploads = [],
            ErrorMessages = [],
        };
        DbContext.Archives.Add(orphanedArchive);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var result = await DbContext.Archives.AnyAsync();

        result.ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_AssignableArchiveExists_AssignsArchiveAndDoesNotInvokeArchiver()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;
        var archiveFilePath = Path.Combine(existingArchiveFolder, "existing.part1.rar");
        await File.WriteAllTextAsync(archiveFilePath, "archive-data");
        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles =
            [
                new ArchiveFile
                {
                    FullFileName = archiveFilePath,
                    Md5Hash = PreviouslyComputedMd5Hash,
                },
            ],
            Uploads = [],
            ErrorMessages = [],
        };
        DbContext.Archives.Add(existingArchive);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.SingleAsync();

        result.ShouldNotBeNull();
        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        archiverMock.Verify(
            a =>
                a.ArchiveAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ProcessAsync_AssignableArchiveWasAlreadyUploadedToSameHoster_AppendsNullByteToArchiveFiles()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;
        var archiveFilePath = Path.Combine(existingArchiveFolder, "existing.part1.rar");
        await File.WriteAllTextAsync(archiveFilePath, "archive-data");
        var originalLength = new FileInfo(archiveFilePath).Length;
        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [new ArchiveFile { FullFileName = archiveFilePath }],
            Uploads = [],
            ErrorMessages = [],
        };
        var previousUpload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            ErrorMessages = [],
        };
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.SingleAsync(u => u.Id == upload.Id);
        var changedArchiveBytes = await File.ReadAllBytesAsync(archiveFilePath);
        var changedArchiveFile = await DbContext.ArchiveFiles.SingleAsync(f =>
            f.FullFileName == archiveFilePath
        );

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        ((long)changedArchiveBytes.Length).ShouldBe(originalLength + 1);
        changedArchiveBytes.Last().ShouldBe((byte)0);
        changedArchiveFile.Md5Hash.ShouldBe(Convert.ToHexString(MD5.HashData(changedArchiveBytes)));
        archiverFactoryMock.Verify(f => f.GetByName("zip"), Times.Once);
    }

    [Test]
    public async Task ProcessAsync_AssignableArchiveWasAlreadyUploadedToSameHoster_TracksHashChangeProgressOfExistingArchive()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;
        var archiveFilePath = Path.Combine(existingArchiveFolder, "existing.part1.rar");
        await File.WriteAllTextAsync(archiveFilePath, "archive-data");
        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [new ArchiveFile { FullFileName = archiveFilePath }],
            Uploads = [],
            ErrorMessages = [],
        };
        var previousUpload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            ErrorMessages = [],
        };
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var hashChangeIdentifier = new TransferIdentifier(
            TransferType.ArchiveHashChange,
            existingArchive.Id
        );
        progressTracker.PlannedFilesPerIdentifier.Keys.ShouldBe([hashChangeIdentifier]);
        progressTracker
            .LastSnapshotPerIdentifier[hashChangeIdentifier]
            .TransferredBytes.ShouldBe(12);
        progressTracker.StoppedIdentifiers.ShouldContain(hashChangeIdentifier);
        progressTracker.GetTrackedIds(TransferType.ArchiveHashChange).ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_AssignableArchiveWasOnlyUploadedToDifferentHoster_DoesNotChangeArchiveFiles()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var otherUploadConfig = await AddUploadConfigAsync(
            upload.UploadConfig.ArchiveConfig,
            hosterClassName: "OtherHoster",
            name: "Other hoster upload"
        );
        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;
        var archiveFilePath = Path.Combine(existingArchiveFolder, "existing.part1.rar");
        await File.WriteAllTextAsync(archiveFilePath, "archive-data");
        var originalLength = new FileInfo(archiveFilePath).Length;
        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles =
            [
                new ArchiveFile
                {
                    FullFileName = archiveFilePath,
                    Md5Hash = PreviouslyComputedMd5Hash,
                },
            ],
            Uploads = [],
            ErrorMessages = [],
        };
        var previousUpload = new Upload
        {
            UploadConfigId = otherUploadConfig.Id,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            ErrorMessages = [],
        };
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.SingleAsync(u => u.Id == upload.Id);

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        new FileInfo(archiveFilePath).Length.ShouldBe(originalLength);
        archiverMock.Verify(
            a =>
                a.ArchiveAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ProcessAsync_MultipleReuploadsUseSameExistingArchive_ChangesArchiveFileHashesOnce()
    {
        // Arrange
        var firstUpload = await AddUploadWaitingForArchiveAsync();
        var secondUploadConfig = await AddUploadConfigAsync(
            firstUpload.UploadConfig.ArchiveConfig,
            hosterClassName: "OtherHoster",
            name: "Other hoster upload"
        );
        var secondUpload = new Upload
        {
            UploadConfigId = secondUploadConfig.Id,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.WaitingForArchive,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
        };
        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;
        var archiveFilePath = Path.Combine(existingArchiveFolder, "existing.part1.rar");
        await File.WriteAllTextAsync(archiveFilePath, "archive-data");
        var originalLength = new FileInfo(archiveFilePath).Length;
        var existingArchive = new Archive
        {
            ArchiveConfigId = firstUpload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [new ArchiveFile { FullFileName = archiveFilePath }],
            Uploads = [],
            ErrorMessages = [],
        };
        var previousFirstHosterUpload = new Upload
        {
            UploadConfigId = firstUpload.UploadConfigId,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            ErrorMessages = [],
        };
        var previousSecondHosterUpload = new Upload
        {
            UploadConfigId = secondUploadConfig.Id,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            ErrorMessages = [],
        };
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.AddRange(
            secondUpload,
            previousFirstHosterUpload,
            previousSecondHosterUpload
        );
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Where(u => u.Id == firstUpload.Id || u.Id == secondUpload.Id)
            .OrderBy(u => u.Id)
            .ToListAsync();
        var changedArchiveBytes = await File.ReadAllBytesAsync(archiveFilePath);

        result.ShouldAllBe(u => u.ArchiveId == existingArchive.Id);
        result.ShouldAllBe(u => u.UploadState == UploadState.Pending);
        ((long)changedArchiveBytes.Length).ShouldBe(originalLength + 1);
        changedArchiveBytes.Last().ShouldBe((byte)0);
        archiverFactoryMock.Verify(f => f.GetByName("zip"), Times.Once);
    }

    [Test]
    public async Task ProcessAsync_ArchiverCannotChangeHashInPlace_CreatesNewArchive()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        upload.UploadConfig.ArchiveConfig.ArchiverName = "7Zip";
        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;
        var firstArchiveFilePath = Path.Combine(existingArchiveFolder, "existing.7z.001");
        var secondArchiveFilePath = Path.Combine(existingArchiveFolder, "existing.7z.002");
        await File.WriteAllTextAsync(firstArchiveFilePath, "first");
        await File.WriteAllTextAsync(secondArchiveFilePath, "second");
        var originalFirstFileLength = new FileInfo(firstArchiveFilePath).Length;
        var originalSecondFileLength = new FileInfo(secondArchiveFilePath).Length;
        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles =
            [
                new ArchiveFile { FullFileName = firstArchiveFilePath },
                new ArchiveFile { FullFileName = secondArchiveFilePath },
            ],
            Uploads = [],
            ErrorMessages = [],
        };
        var previousUpload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            ErrorMessages = [],
        };
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        archiverFactoryMock.Setup(f => f.GetByName("7Zip")).Returns(archiverMock.Object);
        archiverMock.SetupGet(a => a.CanChangeHashInPlace).Returns(false);
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.Is<string>(p => p.StartsWith(archiveFilesBasePath)),
                    "bearcat-release",
                    513,
                    "secret",
                    It.Is<ArchiveOptions>(o => !o.UseCompression && !o.UseSolidArchive),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ArchiveResult(true, ["new-archive.7z.001"], null));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.Archive)
                .ThenInclude(a => a!.ArchiveFiles)
            .SingleAsync(u => u.Id == upload.Id);

        result.ArchiveId.ShouldNotBe(existingArchive.Id);
        result.Archive!.ArchiveState.ShouldBe(ArchiveState.Created);
        result.Archive.ArchiveFiles.Single().FullFileName.ShouldBe("new-archive.7z.001");
        result.UploadState.ShouldBe(UploadState.Pending);
        new FileInfo(firstArchiveFilePath).Length.ShouldBe(originalFirstFileLength);
        new FileInfo(secondArchiveFilePath).Length.ShouldBe(originalSecondFileLength);
        archiverFactoryMock.Verify(f => f.GetByName("7Zip"), Times.Exactly(2));
    }

    [Test]
    public async Task ProcessAsync_AssignableArchiveIsActivelyUploading_SkipsUploadUntilNextRun()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;
        var archiveFilePath = Path.Combine(existingArchiveFolder, "existing.part1.rar");
        await File.WriteAllTextAsync(archiveFilePath, "archive-data");
        var originalLength = new FileInfo(archiveFilePath).Length;
        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [new ArchiveFile { FullFileName = archiveFilePath }],
            Uploads = [],
            ErrorMessages = [],
        };
        var previousUpload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UploadedAt = DateTime.UtcNow.AddHours(-2),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            ErrorMessages = [],
        };
        var activeUpload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Uploading,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
        };
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.AddRange(previousUpload, activeUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.SingleAsync(u => u.Id == upload.Id);

        result.ArchiveId.ShouldBeNull();
        result.UploadState.ShouldBe(UploadState.WaitingForArchive);
        new FileInfo(archiveFilePath).Length.ShouldBe(originalLength);
        (await DbContext.Archives.CountAsync()).ShouldBe(1);
        progressTracker.PlannedFilesPerIdentifier.ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_NoAssignableArchiveExists_CreatesArchiveAndSetsUploadsPending()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        archiverFactoryMock.Setup(f => f.GetByName("zip")).Returns(archiverMock.Object);
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.Is<string>(p => p.StartsWith(archiveFilesBasePath)),
                    "bearcat-release",
                    512,
                    "secret",
                    It.Is<ArchiveOptions>(o => !o.UseCompression && !o.UseSolidArchive),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ArchiveResult(true, ["archive.part1.rar", "archive.part2.rar"], null)
            );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .Include(a => a.Uploads)
            .SingleAsync();

        result.ShouldNotBeNull();
        result.ArchiveState.ShouldBe(ArchiveState.Created);
        result.ArchiveFileSizeMb.ShouldBe(512);
        result.ArchiveFolderPath.ShouldStartWith(archiveFilesBasePath);
        result
            .ArchiveFiles.Select(f => f.FullFileName)
            .ShouldBe(["archive.part1.rar", "archive.part2.rar"]);
        result.Uploads.Single().Id.ShouldBe(upload.Id);
        result.Uploads.Single().UploadState.ShouldBe(UploadState.Pending);
        File.Exists(Path.Combine(releaseFolderPath, "__nonce.txt")).ShouldBeFalse();
        archiverFactoryMock.Verify(f => f.GetByName("zip"), Times.Once);
        archiverMock.Verify(
            a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.Is<string>(p => p.StartsWith(archiveFilesBasePath)),
                    "bearcat-release",
                    512,
                    "secret",
                    It.Is<ArchiveOptions>(o => !o.UseCompression && !o.UseSolidArchive),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessAsync_NoAssignableArchiveExists_TracksPackingProgressOfReleaseFolder()
    {
        // Arrange
        await AddUploadWaitingForArchiveAsync();
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "movie-content");
        Directory.CreateDirectory(Path.Combine(releaseFolderPath, "Sample"));
        await File.WriteAllTextAsync(
            Path.Combine(releaseFolderPath, "Sample", "sample.mkv"),
            "sample"
        );
        SetupArchiverWritingVolumes(
            ("bearcat-release.part1.rar", "first"),
            ("bearcat-release.part2.rar", "second")
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var archiveId = (await DbContext.Archives.SingleAsync()).Id;
        var identifier = new TransferIdentifier(TransferType.ArchiveCreation, archiveId);
        progressTracker
            .PlannedFilesPerIdentifier[identifier]
            .ShouldBe([
                new TransferFile(
                    1,
                    "Bearcat.Release.001",
                    "Main archive",
                    releaseFolderBytesWhilePacking,
                    IsAlreadyTransferred: false
                ),
            ]);
        progressTracker.LastSnapshotPerIdentifier[identifier].TransferredBytes.ShouldBe(11);
        progressTracker.StoppedIdentifiers.ShouldContain(identifier);
        progressTracker.Get(identifier).ShouldBeNull();
    }

    [Test]
    public async Task ProcessAsync_NoAssignableArchiveExists_TracksHashingProgressPerArchiveFileWithUnchangedHashes()
    {
        // Arrange
        await AddUploadWaitingForArchiveAsync();
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "movie-content");
        SetupArchiverWritingVolumes(
            ("bearcat-release.part1.rar", "first"),
            ("bearcat-release.part2.rar", "second")
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var archive = await DbContext.Archives.Include(a => a.ArchiveFiles).SingleAsync();
        var identifier = new TransferIdentifier(TransferType.ArchiveHashing, archive.Id);
        progressTracker
            .PlannedFilesPerIdentifier[identifier]
            .ShouldBe(
                [
                    new TransferFile(
                        1,
                        "bearcat-release.part1.rar",
                        "Main archive",
                        5,
                        IsAlreadyTransferred: false
                    ),
                    new TransferFile(
                        2,
                        "bearcat-release.part2.rar",
                        "Main archive",
                        6,
                        IsAlreadyTransferred: false
                    ),
                ],
                ignoreOrder: true
            );
        var snapshot = progressTracker.LastSnapshotPerIdentifier[identifier];
        snapshot.TransferredBytes.ShouldBe(11);
        snapshot.TotalBytes.ShouldBe(11);
        progressTracker.StoppedIdentifiers.ShouldContain(identifier);
        progressTracker.PlannedFilesPerIdentifier.Keys.ShouldNotContain(
            new TransferIdentifier(TransferType.ArchiveHashChange, archive.Id)
        );
        foreach (var archiveFile in archive.ArchiveFiles)
        {
            var fileBytes = await File.ReadAllBytesAsync(archiveFile.FullFileName);
            fileBytes[^1].ShouldBe((byte)0);
            archiveFile.Md5Hash.ShouldBe(Convert.ToHexString(MD5.HashData(fileBytes)));
        }
    }

    [Test]
    public async Task ProcessAsync_ArchiverCannotChangeHashInPlace_TracksHashingProgressWithoutChangingFiles()
    {
        // Arrange
        archiverMock.SetupGet(a => a.CanChangeHashInPlace).Returns(false);
        await AddUploadWaitingForArchiveAsync();
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "movie-content");
        SetupArchiverWritingVolumes(("bearcat-release.part1.rar", "first"));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var archive = await DbContext.Archives.Include(a => a.ArchiveFiles).SingleAsync();
        var identifier = new TransferIdentifier(TransferType.ArchiveHashing, archive.Id);
        var archiveFile = archive.ArchiveFiles.ShouldHaveSingleItem();
        (await File.ReadAllTextAsync(archiveFile.FullFileName)).ShouldBe("first");
        archiveFile.Md5Hash.ShouldBe(Convert.ToHexString(MD5.HashData("first"u8.ToArray())));
        progressTracker.LastSnapshotPerIdentifier[identifier].TransferredBytes.ShouldBe(5);
        progressTracker.StoppedIdentifiers.ShouldContain(identifier);
    }

    [Test]
    public async Task ProcessAsync_ReleaseContainsSynologyMetadataFolders_RemovesThemBeforeArchiving()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var topLevelMetadataFolder = Directory
            .CreateDirectory(Path.Combine(releaseFolderPath, "@eaDir"))
            .FullName;
        var nestedMediaFolder = Directory
            .CreateDirectory(Path.Combine(releaseFolderPath, "Disc1"))
            .FullName;
        var nestedMetadataFolder = Directory
            .CreateDirectory(Path.Combine(nestedMediaFolder, "@eaDir"))
            .FullName;
        await File.WriteAllTextAsync(Path.Combine(topLevelMetadataFolder, "thumb.jpg"), "junk");
        await File.WriteAllTextAsync(Path.Combine(nestedMetadataFolder, "thumb.jpg"), "junk");

        var metadataFoldersExistedDuringArchiving = true;
        archiverFactoryMock.Setup(f => f.GetByName("zip")).Returns(archiverMock.Object);
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.IsAny<string>(),
                    "bearcat-release",
                    It.IsAny<int>(),
                    "secret",
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() =>
                metadataFoldersExistedDuringArchiving =
                    Directory.Exists(topLevelMetadataFolder)
                    || Directory.Exists(nestedMetadataFolder)
            )
            .ReturnsAsync(new ArchiveResult(true, ["archive.part1.rar"], null));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        metadataFoldersExistedDuringArchiving.ShouldBeFalse();
        Directory.Exists(topLevelMetadataFolder).ShouldBeFalse();
        Directory.Exists(nestedMetadataFolder).ShouldBeFalse();
        Directory.Exists(nestedMediaFolder).ShouldBeTrue();
        upload.UploadState.ShouldBe(UploadState.Pending);
    }

    [Test]
    public async Task ProcessAsync_SolidCompressionStrategy_CreatesSolidCompressedArchive()
    {
        // Arrange
        configurationProviderMock
            .Setup(p =>
                p.GetValue<ArchiveRepackagingConfiguration>(
                    It.IsAny<Expression<Func<ArchiveRepackagingConfiguration, string?>>>()
                )
            )
            .Returns(ArchiveRepackagingStrategies.SolidCompression);
        var upload = await AddUploadWaitingForArchiveAsync();
        archiverFactoryMock.Setup(f => f.GetByName("zip")).Returns(archiverMock.Object);
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.Is<string>(p => p.StartsWith(archiveFilesBasePath)),
                    "bearcat-release",
                    512,
                    "secret",
                    It.Is<ArchiveOptions>(o => o.UseCompression && o.UseSolidArchive),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ArchiveResult(true, ["archive.part1.rar"], null));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.Include(a => a.Uploads).SingleAsync();

        result.ArchiveFileSizeMb.ShouldBe(512);
        result.Uploads.Single().Id.ShouldBe(upload.Id);
    }

    [Test]
    public async Task ProcessAsync_IncrementArchiveFileSizeStrategyWithUnknownPreviousHashes_UsesLastArchiveFileSizePlusOne()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        DbContext.Archives.Add(
            new Archive
            {
                ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
                ArchiveFolderPath = Directory
                    .CreateDirectory(Path.Combine(archiveFilesBasePath, "previous"))
                    .FullName,
                ArchiveState = ArchiveState.MissingFiles,
                ArchiveFileSizeMb = 512,
                CreatedAt = DateTime.UtcNow,
                ArchiveFiles = [new ArchiveFile { FullFileName = "previous.part1.rar" }],
                Uploads = [],
                ErrorMessages = [],
            }
        );
        await DbContext.SaveChangesAsync();

        archiverFactoryMock.Setup(f => f.GetByName("zip")).Returns(archiverMock.Object);
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.Is<string>(p => p.StartsWith(archiveFilesBasePath)),
                    "bearcat-release",
                    513,
                    "secret",
                    It.Is<ArchiveOptions>(o => !o.UseCompression && !o.UseSolidArchive),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ArchiveResult(true, ["archive.part1.rar"], null));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.OrderByDescending(a => a.Id).FirstAsync();

        result.ArchiveFileSizeMb.ShouldBe(513);
    }

    [Test]
    public async Task ProcessAsync_IncrementArchiveFileSizeStrategyWithKnownPreviousHashes_PacksAtBaseArchiveFileSize()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        DbContext.Archives.Add(
            new Archive
            {
                ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
                ArchiveFolderPath = Directory
                    .CreateDirectory(Path.Combine(archiveFilesBasePath, "previous"))
                    .FullName,
                ArchiveState = ArchiveState.MissingFiles,
                ArchiveFileSizeMb = 512,
                CreatedAt = DateTime.UtcNow,
                ArchiveFiles =
                [
                    new ArchiveFile
                    {
                        FullFileName = "previous.part1.rar",
                        Md5Hash = "0123456789ABCDEF0123456789ABCDEF",
                    },
                ],
                Uploads = [],
                ErrorMessages = [],
            }
        );
        await DbContext.SaveChangesAsync();

        archiverFactoryMock.Setup(f => f.GetByName("zip")).Returns(archiverMock.Object);
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.Is<string>(p => p.StartsWith(archiveFilesBasePath)),
                    "bearcat-release",
                    512,
                    "secret",
                    It.Is<ArchiveOptions>(o => !o.UseCompression && !o.UseSolidArchive),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ArchiveResult(true, ["archive.part1.rar"], null));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.OrderByDescending(a => a.Id).FirstAsync();

        result.ArchiveFileSizeMb.ShouldBe(512);
    }

    [TestCase(ArchiveRepackagingStrategies.NonceOnly, false)]
    [TestCase(ArchiveRepackagingStrategies.SolidCompression, true)]
    [TestCase(ArchiveRepackagingStrategies.IncrementArchiveFileSize, false)]
    public async Task ProcessAsync_CreateNonceFileDisabledAndArchiverCannotChangeHashInPlace_UsesLastArchiveFileSizePlusOne(
        string strategy,
        bool expectedSolidCompression
    )
    {
        // Arrange
        SetupArchiveRepackagingStrategy(strategy);
        archiverMock.SetupGet(a => a.CanChangeHashInPlace).Returns(false);
        var upload = await AddUploadWaitingForArchiveAsync();
        upload.UploadConfig.ArchiveConfig.CreateNonceFile = false;
        await AddPreviousArchiveAsync(
            archiveConfigId: upload.UploadConfig.ArchiveConfigId,
            archiveFileSizeMb: 512,
            md5Hash: "0123456789ABCDEF0123456789ABCDEF"
        );
        SetupArchiverWritingVolumes(("bearcat-release.7z.001", "first"));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.OrderByDescending(a => a.Id).FirstAsync();

        result.ArchiveFileSizeMb.ShouldBe(513);
        result.ArchiveState.ShouldBe(ArchiveState.Created);
        archiverMock.Verify(
            a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.IsAny<string>(),
                    "bearcat-release",
                    513,
                    "secret",
                    It.Is<ArchiveOptions>(o =>
                        o.UseCompression == expectedSolidCompression
                        && o.UseSolidArchive == expectedSolidCompression
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessAsync_CreateNonceFileDisabledAndArchiverCannotChangeHashInPlaceWithoutPreviousArchive_UsesConfiguredArchiveFileSize()
    {
        // Arrange
        SetupArchiveRepackagingStrategy(ArchiveRepackagingStrategies.NonceOnly);
        archiverMock.SetupGet(a => a.CanChangeHashInPlace).Returns(false);
        var upload = await AddUploadWaitingForArchiveAsync();
        upload.UploadConfig.ArchiveConfig.CreateNonceFile = false;
        await DbContext.SaveChangesAsync();
        SetupArchiverWritingVolumes(("bearcat-release.7z.001", "first"));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.SingleAsync();

        result.ArchiveFileSizeMb.ShouldBe(512);
    }

    [TestCase(ArchiveRepackagingStrategies.NonceOnly)]
    [TestCase(ArchiveRepackagingStrategies.SolidCompression)]
    public async Task ProcessAsync_CreateNonceFileEnabledAndArchiverCannotChangeHashInPlace_UsesConfiguredArchiveFileSize(
        string strategy
    )
    {
        // Arrange
        SetupArchiveRepackagingStrategy(strategy);
        archiverMock.SetupGet(a => a.CanChangeHashInPlace).Returns(false);
        var upload = await AddUploadWaitingForArchiveAsync();
        await AddPreviousArchiveAsync(
            archiveConfigId: upload.UploadConfig.ArchiveConfigId,
            archiveFileSizeMb: 512,
            md5Hash: "0123456789ABCDEF0123456789ABCDEF"
        );
        SetupArchiverWritingVolumes(("bearcat-release.7z.001", "first"));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.OrderByDescending(a => a.Id).FirstAsync();

        result.ArchiveFileSizeMb.ShouldBe(512);
    }

    [TestCase(ArchiveRepackagingStrategies.NonceOnly, null)]
    [TestCase(ArchiveRepackagingStrategies.NonceOnly, "0123456789ABCDEF0123456789ABCDEF")]
    [TestCase(ArchiveRepackagingStrategies.SolidCompression, null)]
    [TestCase(ArchiveRepackagingStrategies.SolidCompression, "0123456789ABCDEF0123456789ABCDEF")]
    public async Task ProcessAsync_CreateNonceFileDisabledAndArchiverCanChangeHashInPlace_UsesConfiguredArchiveFileSize(
        string strategy,
        string? previousArchiveFileMd5Hash
    )
    {
        // Arrange
        SetupArchiveRepackagingStrategy(strategy);
        var upload = await AddUploadWaitingForArchiveAsync();
        upload.UploadConfig.ArchiveConfig.CreateNonceFile = false;
        await AddPreviousArchiveAsync(
            archiveConfigId: upload.UploadConfig.ArchiveConfigId,
            archiveFileSizeMb: 512,
            md5Hash: previousArchiveFileMd5Hash
        );
        SetupArchiverWritingVolumes(("bearcat-release.part1.rar", "first"));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.OrderByDescending(a => a.Id).FirstAsync();

        result.ArchiveFileSizeMb.ShouldBe(512);
        result.ArchiveState.ShouldBe(ArchiveState.Created);
    }

    [Test]
    public async Task ProcessAsync_AppendedHashCollidesWithKnownHash_AppendsUntilHashIsUnique()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;
        var archiveFilePath = Path.Combine(existingArchiveFolder, "existing.part1.rar");
        var originalBytes = "archive-data"u8.ToArray();
        await File.WriteAllBytesAsync(archiveFilePath, originalBytes);
        var hashAfterFirstAppend = Convert.ToHexString(MD5.HashData([.. originalBytes, (byte)0]));

        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [new ArchiveFile { FullFileName = archiveFilePath }],
            Uploads = [],
            ErrorMessages = [],
        };
        var blockedHashArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = Directory
                .CreateDirectory(Path.Combine(archiveFilesBasePath, "blocked"))
                .FullName,
            ArchiveState = ArchiveState.MissingFiles,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ArchiveFiles =
            [
                new ArchiveFile
                {
                    FullFileName = "blocked.part1.rar",
                    Md5Hash = hashAfterFirstAppend,
                },
            ],
            Uploads = [],
            ErrorMessages = [],
        };
        var previousUpload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            ErrorMessages = [],
        };
        DbContext.Archives.AddRange(existingArchive, blockedHashArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var changedArchiveBytes = await File.ReadAllBytesAsync(archiveFilePath);
        var changedArchiveFile = await DbContext.ArchiveFiles.SingleAsync(f =>
            f.FullFileName == archiveFilePath
        );

        changedArchiveBytes.Length.ShouldBe(originalBytes.Length + 2);
        changedArchiveFile.Md5Hash.ShouldBe(Convert.ToHexString(MD5.HashData(changedArchiveBytes)));
        changedArchiveFile.Md5Hash.ShouldNotBe(hashAfterFirstAppend);
    }

    [Test]
    public async Task ProcessAsync_RestoredArchiveFileHasOlderContent_SkipsHashOnlyKnownFromUploadedFile()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();

        var restoredArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "restored"))
            .FullName;

        var archiveFilePath = Path.Combine(restoredArchiveFolder, "restored.part1.rar");
        var restoredBytes = "archive-data"u8.ToArray();
        await File.WriteAllBytesAsync(archiveFilePath, restoredBytes);
        var restoredHash = Convert.ToHexString(MD5.HashData(restoredBytes));

        var hashUploadedBeforeRestore = Convert.ToHexString(
            MD5.HashData([.. restoredBytes, (byte)0])
        );

        var archiveFile = new ArchiveFile
        {
            FullFileName = archiveFilePath,
            Md5Hash = restoredHash,
            UploadedFiles = [],
        };

        var restoredArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = restoredArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [archiveFile],
            Uploads = [],
            ErrorMessages = [],
        };

        var previousUpload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            Archive = restoredArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            ErrorMessages = [],
            UploadedFiles = [],
        };

        previousUpload.UploadedFiles.Add(
            new UploadedFile
            {
                Upload = previousUpload,
                ArchiveFile = archiveFile,
                HosterFileLink = "https://hoster.example/file",
                Md5Hash = hashUploadedBeforeRestore,
                OnlineState = OnlineState.Offline,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
            }
        );

        DbContext.Archives.Add(restoredArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var changedArchiveBytes = await File.ReadAllBytesAsync(archiveFilePath);
        var changedArchiveFile = await DbContext.ArchiveFiles.SingleAsync(f =>
            f.FullFileName == archiveFilePath
        );

        changedArchiveBytes.Length.ShouldBe(restoredBytes.Length + 2);
        changedArchiveFile.Md5Hash.ShouldBe(Convert.ToHexString(MD5.HashData(changedArchiveBytes)));
        changedArchiveFile.Md5Hash.ShouldNotBe(hashUploadedBeforeRestore);
    }

    [Test]
    public async Task ProcessAsync_ArchiverFails_MarksArchiveAsCreationFailedAndCreatesNotification()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        archiverFactoryMock.Setup(f => f.GetByName("zip")).Returns(archiverMock.Object);
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.Is<string>(p => p.StartsWith(archiveFilesBasePath)),
                    "bearcat-release",
                    512,
                    "secret",
                    It.Is<ArchiveOptions>(o => !o.UseCompression && !o.UseSolidArchive),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ArchiveResult(false, [], ["Could not create archive"]));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Archives.Include(a => a.Uploads)
            .Include(a => a.Notifications)
            .SingleAsync();

        result.ShouldNotBeNull();
        result.ArchiveState.ShouldBe(ArchiveState.CreationFailed);
        result.ErrorMessages.ShouldBe(["Could not create archive"]);
        result.Uploads.Single().Id.ShouldBe(upload.Id);
        result.Uploads.Single().UploadState.ShouldBe(UploadState.WaitingForArchive);
        result.Notifications.Single().NotificationSeverity.ShouldBe(NotificationSeverity.Error);
        result
            .Notifications.Single()
            .Message.ShouldBe("Failed to create archive: Could not create archive");
        File.Exists(Path.Combine(releaseFolderPath, "__nonce.txt")).ShouldBeFalse();
        archiverFactoryMock.Verify(f => f.GetByName("zip"), Times.Once);
        archiverMock.Verify(
            a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.Is<string>(p => p.StartsWith(archiveFilesBasePath)),
                    "bearcat-release",
                    512,
                    "secret",
                    It.Is<ArchiveOptions>(o => !o.UseCompression && !o.UseSolidArchive),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessAsync_MultipleUploadsUseSameArchiveConfig_CreatesSingleArchive()
    {
        // Arrange
        var firstUpload = await AddUploadWaitingForArchiveAsync();
        var secondUpload = new Upload
        {
            UploadConfigId = firstUpload.UploadConfigId,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.WaitingForArchive,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
        };
        DbContext.Uploads.Add(secondUpload);
        await DbContext.SaveChangesAsync();

        archiverFactoryMock.Setup(f => f.GetByName("zip")).Returns(archiverMock.Object);
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.Is<string>(p => p.StartsWith(archiveFilesBasePath)),
                    "bearcat-release",
                    512,
                    "secret",
                    It.Is<ArchiveOptions>(o => !o.UseCompression && !o.UseSolidArchive),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ArchiveResult(true, ["archive.part1.rar"], null));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.Include(a => a.Uploads).SingleAsync();

        result.ShouldNotBeNull();
        result.ArchiveState.ShouldBe(ArchiveState.Created);
        result.Uploads.Select(u => u.Id).Order().ShouldBe([firstUpload.Id, secondUpload.Id]);
        result.Uploads.ShouldAllBe(u => u.UploadState == UploadState.Pending);
        archiverFactoryMock.Verify(f => f.GetByName("zip"), Times.Once);
        archiverMock.Verify(
            a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.Is<string>(p => p.StartsWith(archiveFilesBasePath)),
                    "bearcat-release",
                    512,
                    "secret",
                    It.Is<ArchiveOptions>(o => !o.UseCompression && !o.UseSolidArchive),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessAsync_PreviousUploadHasOnlineFiles_CarriesOverOnlineFilesToReupload()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();

        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;

        var onlineArchiveFilePath = Path.Combine(existingArchiveFolder, "existing.part1.rar");
        var offlineArchiveFilePath = Path.Combine(existingArchiveFolder, "existing.part2.rar");

        await File.WriteAllTextAsync(onlineArchiveFilePath, "online-data");
        await File.WriteAllTextAsync(offlineArchiveFilePath, "offline-data");

        var onlineArchiveFile = new ArchiveFile { FullFileName = onlineArchiveFilePath };
        var offlineArchiveFile = new ArchiveFile { FullFileName = offlineArchiveFilePath };

        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [onlineArchiveFile, offlineArchiveFile],
            Uploads = [],
            ErrorMessages = [],
        };

        var previousUpload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.PartiallyOnline,
            ErrorMessages = [],
            UploadedFiles =
            [
                new UploadedFile
                {
                    ArchiveFile = onlineArchiveFile,
                    HosterFileLink = "https://hoster.example/online",
                    ExternalId = "external-1",
                    HosterFolderId = "source-folder",
                    OnlineState = OnlineState.Online,
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    CheckedAt = DateTime.UtcNow.AddHours(-1),
                },
                new UploadedFile
                {
                    ArchiveFile = offlineArchiveFile,
                    HosterFileLink = "https://hoster.example/offline",
                    ExternalId = "external-2",
                    OnlineState = OnlineState.Offline,
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    CheckedAt = DateTime.UtcNow.AddHours(-1),
                },
            ],
        };
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == upload.Id);

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);

        var carriedFile = result.UploadedFiles.ShouldHaveSingleItem();

        carriedFile.ArchiveFileId.ShouldBe(onlineArchiveFile.Id);
        carriedFile.HosterFileLink.ShouldBe("https://hoster.example/online");
        carriedFile.ExternalId.ShouldBe("external-1");
        carriedFile.HosterFolderId.ShouldBe("source-folder");
        carriedFile.OnlineState.ShouldBe(OnlineState.Online);
    }

    [Test]
    public async Task ProcessAsync_FileOfflineInNewestUploadButOnlineInOlderUpload_DoesNotCarryOverStaleOnlineFile()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();

        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;

        var archiveFilePath = Path.Combine(existingArchiveFolder, "existing.part1.rar");

        await File.WriteAllTextAsync(archiveFilePath, "archive-data");

        var archiveFile = new ArchiveFile { FullFileName = archiveFilePath };

        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [archiveFile],
            Uploads = [],
            ErrorMessages = [],
        };

        var olderUpload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UploadedAt = DateTime.UtcNow.AddHours(-2),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            ErrorMessages = [],
            UploadedFiles =
            [
                new UploadedFile
                {
                    ArchiveFile = archiveFile,
                    HosterFileLink = "https://hoster.example/stale-online",
                    ExternalId = "external-stale",
                    OnlineState = OnlineState.Online,
                    CreatedAt = DateTime.UtcNow.AddHours(-2),
                    CheckedAt = DateTime.UtcNow.AddHours(-2),
                },
            ],
        };

        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(olderUpload);
        await DbContext.SaveChangesAsync();

        var newestUpload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            ErrorMessages = [],
            UploadedFiles =
            [
                new UploadedFile
                {
                    ArchiveFile = archiveFile,
                    HosterFileLink = "https://hoster.example/offline",
                    ExternalId = "external-offline",
                    OnlineState = OnlineState.Offline,
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    CheckedAt = DateTime.UtcNow.AddHours(-1),
                },
            ],
        };

        DbContext.Uploads.Add(newestUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == upload.Id);

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        result.UploadedFiles.ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_PreviousUploadOnDifferentUploadConfig_DoesNotCarryOverOnlineFiles()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();

        var otherUploadConfig = await AddUploadConfigAsync(
            upload.UploadConfig.ArchiveConfig,
            hosterClassName: "OtherHoster",
            name: "Other hoster upload"
        );

        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;

        var archiveFilePath = Path.Combine(existingArchiveFolder, "existing.part1.rar");

        await File.WriteAllTextAsync(archiveFilePath, "archive-data");

        var archiveFile = new ArchiveFile { FullFileName = archiveFilePath };

        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [archiveFile],
            Uploads = [],
            ErrorMessages = [],
        };

        var previousUpload = new Upload
        {
            UploadConfigId = otherUploadConfig.Id,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            ErrorMessages = [],
            UploadedFiles =
            [
                new UploadedFile
                {
                    ArchiveFile = archiveFile,
                    HosterFileLink = "https://other-hoster.example/online",
                    ExternalId = "external-1",
                    OnlineState = OnlineState.Online,
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    CheckedAt = DateTime.UtcNow.AddHours(-1),
                },
            ],
        };

        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == upload.Id);

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        result.UploadedFiles.ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_ArchiveConfigHasAdditionalArchiveContents_PlacesContentsInsideReleaseFolderOnlyWhileArchiving()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "movie");
        var sourceFilePath = Path.Combine(additionalContentSourcePath, "Buy-Premium.url");
        await File.WriteAllTextAsync(sourceFilePath, "premium");
        var sourceFolderPath = Directory
            .CreateDirectory(Path.Combine(additionalContentSourcePath, "Extras"))
            .FullName;
        await File.WriteAllTextAsync(Path.Combine(sourceFolderPath, "readme.txt"), "readme");
        var nestedSourceFolderPath = Directory
            .CreateDirectory(Path.Combine(sourceFolderPath, "Nested"))
            .FullName;
        await File.WriteAllTextAsync(Path.Combine(nestedSourceFolderPath, "info.txt"), "info");
        await AddUploadWaitingForArchiveAsync([
            CreatePathContent("Premium link", sourceFilePath),
            CreatePathContent("Extras folder", sourceFolderPath + Path.DirectorySeparatorChar),
            CreateTextFileContent("Mirror text", "Mirror.txt", "mirror"),
        ]);
        List<string> releaseFolderEntriesDuringArchiving = [];
        List<string> persistedEntryNamesDuringArchiving = [];
        var nestedFileContentDuringArchiving = string.Empty;
        SetupArchiver(
            async () =>
            {
                releaseFolderEntriesDuringArchiving = GetRelativePathsInReleaseFolder();
                persistedEntryNamesDuringArchiving =
                    await LoadPersistedReleaseFolderEntriesCopiedForPackingAsync();
                nestedFileContentDuringArchiving = await File.ReadAllTextAsync(
                    Path.Combine(releaseFolderPath, "Extras", "Nested", "info.txt")
                );
            },
            new ArchiveResult(true, ["archive.part1.rar"], null)
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.SingleAsync();

        releaseFolderEntriesDuringArchiving.ShouldBe(
            [
                "movie.mkv",
                "__nonce.txt",
                "Buy-Premium.url",
                "Extras",
                Path.Combine("Extras", "readme.txt"),
                Path.Combine("Extras", "Nested"),
                Path.Combine("Extras", "Nested", "info.txt"),
                "Mirror.txt",
            ],
            ignoreOrder: true
        );
        persistedEntryNamesDuringArchiving.ShouldBe(
            ["__nonce.txt", "Buy-Premium.url", "Extras", "Mirror.txt"],
            ignoreOrder: true
        );
        nestedFileContentDuringArchiving.ShouldBe("info");
        GetRelativePathsInReleaseFolder().ShouldBe(["movie.mkv"]);
        File.Exists(sourceFilePath).ShouldBeTrue();
        File.Exists(Path.Combine(nestedSourceFolderPath, "info.txt")).ShouldBeTrue();
        result.ArchiveState.ShouldBe(ArchiveState.Created);
        result.ReleaseFolderEntriesCopiedForPacking.ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_AdditionalTextFile_WritesUtf8WithByteOrderMarkAndCrlfLineEndings()
    {
        // Arrange
        await AddUploadWaitingForArchiveAsync([
            CreateTextFileContent("Mirror text", "Mirror.txt", "first\nsecond\r\nthird\rfourth ü"),
        ]);
        byte[] textFileBytesDuringArchiving = [];
        SetupArchiver(
            async () =>
                textFileBytesDuringArchiving = await File.ReadAllBytesAsync(
                    Path.Combine(releaseFolderPath, "Mirror.txt")
                ),
            new ArchiveResult(true, ["archive.part1.rar"], null)
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        textFileBytesDuringArchiving.ShouldBe([
            .. Encoding.UTF8.GetPreamble(),
            .. Encoding.UTF8.GetBytes("first\r\nsecond\r\nthird\r\nfourth ü"),
        ]);
        File.Exists(Path.Combine(releaseFolderPath, "Mirror.txt")).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_ReleaseFolderContainsNonceFromOlderVersion_OverwritesAndDeletesNonce()
    {
        // Arrange
        var noncePath = Path.Combine(releaseFolderPath, "__nonce.txt");
        await File.WriteAllTextAsync(noncePath, "old-nonce");
        await AddUploadWaitingForArchiveAsync();
        var nonceDuringArchiving = string.Empty;
        SetupArchiver(
            async () => nonceDuringArchiving = await File.ReadAllTextAsync(noncePath),
            new ArchiveResult(true, ["archive.part1.rar"], null)
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.SingleAsync();

        Guid.TryParse(nonceDuringArchiving, out _).ShouldBeTrue();
        File.Exists(noncePath).ShouldBeFalse();
        result.ArchiveState.ShouldBe(ArchiveState.Created);
    }

    [Test]
    public async Task ProcessAsync_CreateNonceFileDisabled_DoesNotWriteNonceFileIntoReleaseFolder()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "movie");
        var upload = await AddUploadWaitingForArchiveAsync([
            CreateTextFileContent("Mirror text", "Mirror.txt", "mirror"),
        ]);
        upload.UploadConfig.ArchiveConfig.CreateNonceFile = false;
        await DbContext.SaveChangesAsync();
        List<string> releaseFolderEntriesDuringArchiving = [];
        List<string> persistedEntryNamesDuringArchiving = [];
        SetupArchiver(
            async () =>
            {
                releaseFolderEntriesDuringArchiving = GetRelativePathsInReleaseFolder();
                persistedEntryNamesDuringArchiving =
                    await LoadPersistedReleaseFolderEntriesCopiedForPackingAsync();
            },
            new ArchiveResult(true, ["archive.part1.rar"], null)
        );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.SingleAsync();

        releaseFolderEntriesDuringArchiving.ShouldBe(
            ["movie.mkv", "Mirror.txt"],
            ignoreOrder: true
        );
        persistedEntryNamesDuringArchiving.ShouldBe(["Mirror.txt"]);
        GetRelativePathsInReleaseFolder().ShouldBe(["movie.mkv"]);
        result.ArchiveState.ShouldBe(ArchiveState.Created);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ProcessAsync_PackReleaseFolderAsRootFolderConfigured_PassesOptionToArchiver(
        bool packReleaseFolderAsRootFolder
    )
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        upload.UploadConfig.ArchiveConfig.PackReleaseFolderAsRootFolder =
            packReleaseFolderAsRootFolder;
        await DbContext.SaveChangesAsync();
        SetupArchiverWritingVolumes(("bearcat-release.part1.rar", "first"));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        archiverMock.Verify(
            a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.IsAny<string>(),
                    "bearcat-release",
                    512,
                    "secret",
                    It.Is<ArchiveOptions>(o =>
                        o.PackSourceFolderAsRootFolder == packReleaseFolderAsRootFolder
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessAsync_AdditionalContentCollidesWithExistingReleaseFile_FailsWithoutTouchingReleaseFolder()
    {
        // Arrange
        var userFilePath = Path.Combine(releaseFolderPath, "Buy-Premium.txt");
        await File.WriteAllTextAsync(userFilePath, "user content");
        var sourceFilePath = Path.Combine(additionalContentSourcePath, "Extras.txt");
        await File.WriteAllTextAsync(sourceFilePath, "extras");
        var upload = await AddUploadWaitingForArchiveAsync([
            CreateTextFileContent("Premium text", "buy-premium.TXT", "ad"),
            CreatePathContent("Extras file", sourceFilePath),
        ]);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Archives.Include(a => a.Uploads)
            .Include(a => a.Notifications)
            .SingleAsync();

        result.ArchiveState.ShouldBe(ArchiveState.CreationFailed);
        result.ErrorMessages.ShouldBe([
            $"Cannot place additional archive content \"Premium text\" into the release folder because \"buy-premium.TXT\" already exists in \"{releaseFolderPath}\".",
        ]);
        result.ReleaseFolderEntriesCopiedForPacking.ShouldBeEmpty();
        result.Uploads.Single().Id.ShouldBe(upload.Id);
        result.Uploads.Single().UploadState.ShouldBe(UploadState.WaitingForArchive);
        result
            .Notifications.Single()
            .Message.ShouldBe($"Failed to create archive: {result.ErrorMessages.Single()}");
        GetRelativePathsInReleaseFolder().ShouldBe(["Buy-Premium.txt"]);
        (await File.ReadAllTextAsync(userFilePath)).ShouldBe("user content");
        archiverMock.Verify(
            a =>
                a.ArchiveAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ProcessAsync_AdditionalContentsShareEntryName_FailsWithoutTouchingReleaseFolder()
    {
        // Arrange
        var sourceFilePath = Path.Combine(additionalContentSourcePath, "INFO.txt");
        await File.WriteAllTextAsync(sourceFilePath, "info");
        await AddUploadWaitingForArchiveAsync([
            CreateTextFileContent("Info text", "Info.txt", "info"),
            CreatePathContent("Info file", sourceFilePath),
        ]);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.SingleAsync();

        result.ArchiveState.ShouldBe(ArchiveState.CreationFailed);
        var errorMessage = result.ErrorMessages.ShouldHaveSingleItem();
        errorMessage.ShouldStartWith("Cannot place additional archive content ");
        errorMessage.ShouldContain("additional archive content \"Info text\"");
        errorMessage.ShouldContain("additional archive content \"Info file\"");
        errorMessage.ShouldContain("into the release folder because they would all be named");
        result.ReleaseFolderEntriesCopiedForPacking.ShouldBeEmpty();
        GetRelativePathsInReleaseFolder().ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_AdditionalPathDoesNotExist_FailsWithoutTouchingReleaseFolder()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "movie");
        var missingSourcePath = Path.Combine(additionalContentSourcePath, "missing.txt");
        await AddUploadWaitingForArchiveAsync([
            CreatePathContent("Missing file", missingSourcePath),
            CreateTextFileContent("Mirror text", "Mirror.txt", "mirror"),
        ]);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.SingleAsync();

        result.ArchiveState.ShouldBe(ArchiveState.CreationFailed);
        result.ErrorMessages.ShouldBe([
            $"Cannot place additional archive content \"Missing file\" into the release folder because its source path \"{missingSourcePath}\" does not exist.",
        ]);
        result.ReleaseFolderEntriesCopiedForPacking.ShouldBeEmpty();
        GetRelativePathsInReleaseFolder().ShouldBe(["movie.mkv"]);
    }

    [Test]
    public async Task ProcessAsync_ArchiveFilesBasePathDoesNotExist_FailsUploadsWithoutCreatingIt()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "movie");
        var upload = await AddUploadWaitingForArchiveAsync();
        Directory.Delete(archiveFilesBasePath, recursive: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.SingleAsync(u => u.Id == upload.Id);

        result.UploadState.ShouldBe(UploadState.Failed);
        (await DbContext.Archives.AnyAsync()).ShouldBeFalse();
        Directory.Exists(archiveFilesBasePath).ShouldBeFalse();

        var notification = await DbContext.Notifications.SingleAsync();
        notification.NotificationKind.ShouldBe(NotificationKind.ArchiveCreationFailed);
        notification.Message.ShouldContain($"Archive folder {archiveFilesBasePath} does not exist");
    }

    [Test]
    public async Task ProcessAsync_AdditionalFolderContainsReleaseFolder_FailsWithoutTouchingReleaseFolder()
    {
        // Arrange
        await AddUploadWaitingForArchiveAsync([
            CreatePathContent("Temp root folder", tempRootPath),
        ]);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.SingleAsync();

        result.ArchiveState.ShouldBe(ArchiveState.CreationFailed);
        result.ErrorMessages.ShouldBe([
            $"Cannot place additional archive content \"Temp root folder\" into the release folder because its source path \"{tempRootPath}\" contains the release folder \"{releaseFolderPath}\".",
        ]);
        GetRelativePathsInReleaseFolder().ShouldBeEmpty();
    }

    [Test]
    public async Task ProcessAsync_CopyingAdditionalContentFails_MarksArchiveAsCreationFailedAndRestoresReleaseFolder()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "movie");
        var sourceFilePath = Path.Combine(additionalContentSourcePath, "Buy-Premium.url");
        await File.WriteAllTextAsync(sourceFilePath, "premium");
        var upload = await AddUploadWaitingForArchiveAsync([
            CreateTextFileContent("Mirror text", "Mirror.txt", "mirror"),
            CreatePathContent("Premium link", sourceFilePath),
        ]);
        var fileSystemServiceMock = CreateFileSystemServiceMockDelegatingToRealFileSystem();
        fileSystemServiceMock
            .Setup(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new UnauthorizedAccessException("Access denied"));
        var failingCopyService = CreateService(fileSystemServiceMock.Object);

        // Act
        await failingCopyService.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Archives.Include(a => a.Uploads)
            .Include(a => a.Notifications)
            .SingleAsync();

        result.ArchiveState.ShouldBe(ArchiveState.CreationFailed);
        result.ErrorMessages.ShouldBe([
            "Cannot place additional archive content \"Premium link\" into the release folder as \"Buy-Premium.url\": Access denied",
        ]);
        result.ReleaseFolderEntriesCopiedForPacking.ShouldBeEmpty();
        result.Uploads.Single().Id.ShouldBe(upload.Id);
        result.Uploads.Single().UploadState.ShouldBe(UploadState.WaitingForArchive);
        result
            .Notifications.Single()
            .Message.ShouldBe($"Failed to create archive: {result.ErrorMessages.Single()}");
        GetRelativePathsInReleaseFolder().ShouldBe(["movie.mkv"]);
        archiverMock.Verify(
            a =>
                a.ArchiveAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ProcessAsync_ArchiverThrows_DeletesCopiedEntriesAndClearsPersistedEntryNames()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "movie");
        await AddUploadWaitingForArchiveAsync([
            CreateTextFileContent("Mirror text", "Mirror.txt", "mirror"),
        ]);
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.IsAny<string>(),
                    "bearcat-release",
                    It.IsAny<int>(),
                    "secret",
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new IOException("Disk full"));

        // Act
        await Should.ThrowAsync<IOException>(() => service.ProcessAsync(CancellationToken.None));

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.SingleAsync();

        result.ArchiveState.ShouldBe(ArchiveState.Creating);
        result.ReleaseFolderEntriesCopiedForPacking.ShouldBeEmpty();
        GetRelativePathsInReleaseFolder().ShouldBe(["movie.mkv"]);
    }

    [Test]
    public async Task ProcessAsync_InterruptedArchiveWithCopiedEntries_DeletesEntriesAndRecoversArchive()
    {
        // Arrange
        var archiveConfig = await AddArchiveConfigAsync();
        await CreateReleaseFolderEntriesLeftBehindByCrashAsync();
        var archiveFolderPath = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "interrupted"))
            .FullName;
        var archiveFilePath = Path.Combine(archiveFolderPath, "interrupted.part1.rar");
        await File.WriteAllTextAsync(archiveFilePath, "archive-data");
        DbContext.Archives.Add(
            new Archive
            {
                ArchiveConfigId = archiveConfig.Id,
                ArchiveFolderPath = archiveFolderPath,
                ArchiveState = ArchiveState.Creating,
                ArchiveFileSizeMb = archiveConfig.ArchiveFileSizeMb,
                CreatedAt = DateTime.UtcNow,
                ArchiveFiles = [new ArchiveFile { FullFileName = archiveFilePath }],
                Uploads = [],
                ErrorMessages = [],
                ReleaseFolderEntriesCopiedForPacking = ["__nonce.txt", "Extras", "Mirror.txt"],
            }
        );
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Archives.SingleAsync();

        result.ArchiveState.ShouldBe(ArchiveState.Created);
        result.ReleaseFolderEntriesCopiedForPacking.ShouldBeEmpty();
        GetRelativePathsInReleaseFolder().ShouldBe(["movie.mkv"]);
    }

    [Test]
    public async Task ProcessAsync_InterruptedArchiveWithIncompleteFilesAndCopiedEntries_DeletesEntriesAndArchive()
    {
        // Arrange
        var archiveConfig = await AddArchiveConfigAsync();
        await CreateReleaseFolderEntriesLeftBehindByCrashAsync();
        DbContext.Archives.Add(
            new Archive
            {
                ArchiveConfigId = archiveConfig.Id,
                ArchiveFolderPath = Path.Combine(archiveFilesBasePath, "interrupted"),
                ArchiveState = ArchiveState.Creating,
                ArchiveFileSizeMb = archiveConfig.ArchiveFileSizeMb,
                CreatedAt = DateTime.UtcNow,
                ArchiveFiles = [],
                Uploads = [],
                ErrorMessages = [],
                ReleaseFolderEntriesCopiedForPacking = ["__nonce.txt", "Extras", "Mirror.txt"],
            }
        );
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var result = await DbContext.Archives.AnyAsync();

        result.ShouldBeFalse();
        GetRelativePathsInReleaseFolder().ShouldBe(["movie.mkv"]);
    }

    [Test]
    public async Task ProcessAsync_UserCancelsArchiveCreationWhilePacking_DeletesArchiveAndCancelsWaitingUploads()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "movie");
        SetupArchiverRequestingUserCancellationWhilePacking();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await AssertArchiveWasDeletedAndUploadWasCanceledAsync(upload.Id);
        GetRelativePathsInReleaseFolder().ShouldBe(["movie.mkv"]);
    }

    [Test]
    public async Task ProcessAsync_UserCancelsArchiveCreationWhileHashingAfterPacking_DeletesArchiveAndCancelsWaitingUploads()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "movie");
        SetupArchiverWritingVolumes(
            ("bearcat-release.part1.rar", "first"),
            ("bearcat-release.part2.rar", "second")
        );
        progressTracker.CallbackAfterBeginFile = (identifier, _) =>
        {
            if (identifier.Type == TransferType.ArchiveHashing)
            {
                cancellationRegistry.RequestCancellation(
                    new TransferIdentifier(TransferType.ArchiveCreation, identifier.Id)
                );
                userCanceledArchiveId = identifier.Id;
            }
        };

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await AssertArchiveWasDeletedAndUploadWasCanceledAsync(upload.Id);
        progressTracker.PlannedFilesPerIdentifier.Keys.ShouldContain(identifier =>
            identifier.Type == TransferType.ArchiveHashing
        );
    }

    [Test]
    public async Task ProcessAsync_HostShutsDownWhilePacking_KeepsArchiveCreatingAndUploadsAssigned()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        using var hostStoppingTokenSource = new CancellationTokenSource();
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.IsAny<string>(),
                    "bearcat-release",
                    It.IsAny<int>(),
                    "secret",
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (
                    string _,
                    string _,
                    string _,
                    int _,
                    string? _,
                    ArchiveOptions _,
                    CancellationToken cancellationToken
                ) =>
                {
                    await hostStoppingTokenSource.CancelAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    return new ArchiveResult(true, [], null);
                }
            );

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() =>
            service.ProcessAsync(hostStoppingTokenSource.Token)
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var archive = await DbContext.Archives.SingleAsync();
        var result = await DbContext.Uploads.SingleAsync(u => u.Id == upload.Id);
        var notificationCount = await DbContext.Notifications.CountAsync();

        archive.ArchiveState.ShouldBe(ArchiveState.Creating);
        Directory.Exists(archive.ArchiveFolderPath).ShouldBeTrue();
        result.ArchiveId.ShouldBe(archive.Id);
        result.UploadState.ShouldBe(UploadState.WaitingForArchive);
        notificationCount.ShouldBe(0);
        cancellationRegistry
            .RequestCancellation(new TransferIdentifier(TransferType.ArchiveCreation, archive.Id))
            .ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_UserCancelsFirstOfTwoArchiveCreations_CreatesSecondArchiveInSameRun()
    {
        // Arrange
        var canceledUpload = await AddUploadWaitingForArchiveAsync();
        var createdUpload = await AddUploadWaitingForArchiveAsync();
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "movie");
        var archiveCallCount = 0;
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.IsAny<string>(),
                    "bearcat-release",
                    It.IsAny<int>(),
                    "secret",
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (
                    string _,
                    string destinationPath,
                    string _,
                    int _,
                    string? _,
                    ArchiveOptions _,
                    CancellationToken cancellationToken
                ) =>
                {
                    archiveCallCount++;
                    var filePath = Path.Combine(destinationPath, "bearcat-release.part1.rar");
                    await File.WriteAllTextAsync(filePath, "archive", CancellationToken.None);

                    if (archiveCallCount == 1)
                    {
                        await RequestUserCancellationOfArchiveInFolderAsync(destinationPath);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    return new ArchiveResult(true, [filePath], null);
                }
            );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var canceledResult = await DbContext.Uploads.SingleAsync(u => u.Id == canceledUpload.Id);
        var createdResult = await DbContext
            .Uploads.Include(u => u.Archive)
            .SingleAsync(u => u.Id == createdUpload.Id);

        archiveCallCount.ShouldBe(2);
        canceledResult.ArchiveId.ShouldBeNull();
        canceledResult.UploadState.ShouldBe(UploadState.Canceled);
        createdResult.Archive.ShouldNotBeNull();
        createdResult.Archive.ArchiveState.ShouldBe(ArchiveState.Created);
        createdResult.UploadState.ShouldBe(UploadState.Pending);
    }

    [Test]
    public async Task ProcessAsync_UserCancelsHashChangeOfExistingArchive_KeepsArchiveAndCancelsWaitingUploads()
    {
        // Arrange
        configurationProviderMock
            .Setup(p =>
                p.GetValue<ArchiveRepackagingConfiguration>(
                    It.IsAny<Expression<Func<ArchiveRepackagingConfiguration, int>>>()
                )
            )
            .Returns(1);
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;
        var changedFilePath = Path.Combine(existingArchiveFolder, "existing.part1.rar");
        var interruptedFilePath = Path.Combine(existingArchiveFolder, "existing.part2.rar");
        var untouchedFilePath = Path.Combine(existingArchiveFolder, "existing.part3.rar");
        await File.WriteAllTextAsync(changedFilePath, "first");
        await File.WriteAllTextAsync(interruptedFilePath, "second");
        await File.WriteAllTextAsync(untouchedFilePath, "third");
        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles =
            [
                new ArchiveFile
                {
                    FullFileName = changedFilePath,
                    Md5Hash = ComputeMd5Hash(changedFilePath),
                },
                new ArchiveFile
                {
                    FullFileName = interruptedFilePath,
                    Md5Hash = ComputeMd5Hash(interruptedFilePath),
                },
                new ArchiveFile
                {
                    FullFileName = untouchedFilePath,
                    Md5Hash = ComputeMd5Hash(untouchedFilePath),
                },
            ],
            Uploads = [],
            ErrorMessages = [],
        };
        var previousUpload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            ErrorMessages = [],
        };
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();
        var changedFileHashBeforeChange = ComputeMd5Hash(changedFilePath);
        var untouchedFileHash = ComputeMd5Hash(untouchedFilePath);
        progressTracker.CallbackAfterBeginFile = (identifier, fileId) =>
        {
            if (identifier.Type == TransferType.ArchiveHashChange && fileId == 2)
            {
                cancellationRegistry.RequestCancellation(
                    new TransferIdentifier(TransferType.ArchiveCreation, identifier.Id)
                );
            }
        };

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var archive = await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .SingleAsync(a => a.Id == existingArchive.Id);
        var result = await DbContext.Uploads.SingleAsync(u => u.Id == upload.Id);
        var notification = await DbContext.Notifications.SingleAsync(n => n.UploadId == upload.Id);
        var filesByName = archive.ArchiveFiles.ToDictionary(f => f.FullFileName);

        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        filesByName[changedFilePath].Md5Hash.ShouldBe(ComputeMd5Hash(changedFilePath));
        filesByName[changedFilePath].Md5Hash.ShouldNotBe(changedFileHashBeforeChange);
        filesByName[interruptedFilePath].Md5Hash.ShouldBeNull();
        filesByName[untouchedFilePath].Md5Hash.ShouldBe(untouchedFileHash);
        (await File.ReadAllTextAsync(untouchedFilePath)).ShouldBe("third");
        result.ArchiveId.ShouldBeNull();
        result.UploadState.ShouldBe(UploadState.Canceled);
        notification.NotificationKind.ShouldBe(NotificationKind.UploadCanceled);
        cancellationRegistry
            .RequestCancellation(
                new TransferIdentifier(TransferType.ArchiveCreation, existingArchive.Id)
            )
            .ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_HostShutsDownWhileChangingHashesOfExistingArchive_SavesChangedHashesAndKeepsUploadsWaiting()
    {
        // Arrange
        configurationProviderMock
            .Setup(p =>
                p.GetValue<ArchiveRepackagingConfiguration>(
                    It.IsAny<Expression<Func<ArchiveRepackagingConfiguration, int>>>()
                )
            )
            .Returns(1);
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;
        var changedFilePath = Path.Combine(existingArchiveFolder, "existing.part1.rar");
        var interruptedFilePath = Path.Combine(existingArchiveFolder, "existing.part2.rar");
        var untouchedFilePath = Path.Combine(existingArchiveFolder, "existing.part3.rar");
        await File.WriteAllTextAsync(changedFilePath, "first");
        await File.WriteAllTextAsync(interruptedFilePath, "second");
        await File.WriteAllTextAsync(untouchedFilePath, "third");
        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles =
            [
                new ArchiveFile
                {
                    FullFileName = changedFilePath,
                    Md5Hash = ComputeMd5Hash(changedFilePath),
                },
                new ArchiveFile
                {
                    FullFileName = interruptedFilePath,
                    Md5Hash = ComputeMd5Hash(interruptedFilePath),
                },
                new ArchiveFile
                {
                    FullFileName = untouchedFilePath,
                    Md5Hash = ComputeMd5Hash(untouchedFilePath),
                },
            ],
            Uploads = [],
            ErrorMessages = [],
        };
        var previousUpload = new Upload
        {
            UploadConfigId = upload.UploadConfigId,
            Archive = existingArchive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Offline,
            ErrorMessages = [],
        };
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();
        var changedFileHashBeforeChange = ComputeMd5Hash(changedFilePath);
        var untouchedFileHash = ComputeMd5Hash(untouchedFilePath);
        using var hostShutdown = new CancellationTokenSource();
        progressTracker.CallbackAfterBeginFile = (identifier, fileId) =>
        {
            if (identifier.Type == TransferType.ArchiveHashChange && fileId == 2)
            {
                hostShutdown.Cancel();
            }
        };

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() =>
            service.ProcessAsync(hostShutdown.Token)
        );

        // Assert
        DbContext.ChangeTracker.Clear();
        var archive = await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .SingleAsync(a => a.Id == existingArchive.Id);
        var result = await DbContext.Uploads.SingleAsync(u => u.Id == upload.Id);
        var filesByName = archive.ArchiveFiles.ToDictionary(f => f.FullFileName);

        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        filesByName[changedFilePath].Md5Hash.ShouldBe(ComputeMd5Hash(changedFilePath));
        filesByName[changedFilePath].Md5Hash.ShouldNotBe(changedFileHashBeforeChange);
        filesByName[interruptedFilePath].Md5Hash.ShouldBeNull();
        filesByName[untouchedFilePath].Md5Hash.ShouldBe(untouchedFileHash);
        (await File.ReadAllTextAsync(untouchedFilePath)).ShouldBe("third");
        result.ArchiveId.ShouldBeNull();
        result.UploadState.ShouldBe(UploadState.WaitingForArchive);
        (await DbContext.Notifications.AnyAsync(n => n.UploadId == upload.Id)).ShouldBeFalse();
        cancellationRegistry
            .RequestCancellation(
                new TransferIdentifier(TransferType.ArchiveCreation, existingArchive.Id)
            )
            .ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_AssignableArchiveHasFileWithoutHash_ChangesHashOfThatFileBeforeAssigningArchive()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;
        var fileWithoutHashPath = Path.Combine(existingArchiveFolder, "existing.part1.rar");
        var fileWithHashPath = Path.Combine(existingArchiveFolder, "existing.part2.rar");
        await File.WriteAllTextAsync(fileWithoutHashPath, "first");
        await File.WriteAllTextAsync(fileWithHashPath, "second");
        var fileWithHashHash = ComputeMd5Hash(fileWithHashPath);
        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles =
            [
                new ArchiveFile { FullFileName = fileWithoutHashPath },
                new ArchiveFile { FullFileName = fileWithHashPath, Md5Hash = fileWithHashHash },
            ],
            Uploads = [],
            ErrorMessages = [],
        };
        DbContext.Archives.Add(existingArchive);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var archive = await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .SingleAsync(a => a.Id == existingArchive.Id);
        var result = await DbContext.Uploads.SingleAsync(u => u.Id == upload.Id);

        var filesByName = archive.ArchiveFiles.ToDictionary(f => f.FullFileName);
        var changedFileBytes = await File.ReadAllBytesAsync(fileWithoutHashPath);

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        changedFileBytes.ShouldBe([.. "first"u8.ToArray(), (byte)0]);
        filesByName[fileWithoutHashPath]
            .Md5Hash.ShouldBe(Convert.ToHexString(MD5.HashData(changedFileBytes)));
        (await File.ReadAllTextAsync(fileWithHashPath)).ShouldBe("second");
        filesByName[fileWithHashPath].Md5Hash.ShouldBe(fileWithHashHash);
        progressTracker.PlannedFilesPerIdentifier.Keys.ShouldContain(
            new TransferIdentifier(TransferType.ArchiveHashChange, existingArchive.Id)
        );
    }

    [Test]
    public async Task ProcessAsync_AssignableArchiveHasFileWithoutHashAndArchiverCannotChangeHashInPlace_AssignsArchiveWithoutChangingFiles()
    {
        // Arrange
        archiverMock.SetupGet(a => a.CanChangeHashInPlace).Returns(false);
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
            .FullName;
        var fileWithoutHashPath = Path.Combine(existingArchiveFolder, "existing.7z.001");
        await File.WriteAllTextAsync(fileWithoutHashPath, "first");
        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = existingArchiveFolder,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [new ArchiveFile { FullFileName = fileWithoutHashPath }],
            Uploads = [],
            ErrorMessages = [],
        };
        DbContext.Archives.Add(existingArchive);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var archive = await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .SingleAsync(a => a.Id == existingArchive.Id);
        var result = await DbContext.Uploads.SingleAsync(u => u.Id == upload.Id);

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        (await File.ReadAllTextAsync(fileWithoutHashPath)).ShouldBe("first");
        archive.ArchiveFiles.Single().Md5Hash.ShouldBeNull();
        progressTracker.PlannedFilesPerIdentifier.Keys.ShouldNotContain(
            new TransferIdentifier(TransferType.ArchiveHashChange, existingArchive.Id)
        );
    }

    [Test]
    public async Task ProcessAsync_ReuploadWithFilesStillOnline_ChangesHashesOnlyOfFilesToUpload()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = CreateExistingArchiveFolder();
        var onlineArchiveFile = await CreateArchiveFileWithHashAsync(
            Path.Combine(existingArchiveFolder, "existing.part1.rar"),
            "online"
        );
        var offlineArchiveFile = await CreateArchiveFileWithHashAsync(
            Path.Combine(existingArchiveFolder, "existing.part2.rar"),
            "offline"
        );
        var onlineFileHash = onlineArchiveFile.Md5Hash;
        var existingArchive = CreateExistingArchive(
            archiveConfigId: upload.UploadConfig.ArchiveConfigId,
            archiveFolderPath: existingArchiveFolder,
            archiveFiles: [onlineArchiveFile, offlineArchiveFile]
        );
        var previousUpload = CreatePreviousUpload(
            uploadConfigId: upload.UploadConfigId,
            archive: existingArchive,
            uploadState: UploadState.Completed,
            (onlineArchiveFile, OnlineState.Online),
            (offlineArchiveFile, OnlineState.Offline)
        );
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == upload.Id);
        var archiveFilesById = await DbContext.ArchiveFiles.ToDictionaryAsync(f => f.Id);
        var changedOfflineFileBytes = await File.ReadAllBytesAsync(offlineArchiveFile.FullFileName);

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        result.UploadedFiles.ShouldHaveSingleItem().ArchiveFileId.ShouldBe(onlineArchiveFile.Id);
        (await File.ReadAllTextAsync(onlineArchiveFile.FullFileName)).ShouldBe("online");
        archiveFilesById[onlineArchiveFile.Id].Md5Hash.ShouldBe(onlineFileHash);
        changedOfflineFileBytes.ShouldBe([.. "offline"u8.ToArray(), (byte)0]);
        archiveFilesById[offlineArchiveFile.Id]
            .Md5Hash.ShouldBe(Convert.ToHexString(MD5.HashData(changedOfflineFileBytes)));
        progressTracker
            .PlannedFilesPerIdentifier[
                new TransferIdentifier(TransferType.ArchiveHashChange, existingArchive.Id)
            ]
            .ShouldHaveSingleItem()
            .FileName.ShouldBe("existing.part2.rar");
    }

    [Test]
    public async Task ProcessAsync_ReuploadToHosterThatAlwaysReuploadsAllFiles_ChangesHashesOfAllFiles()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        upload.UploadConfig.HosterRegistration.AlwaysReuploadAllFiles = true;
        var existingArchiveFolder = CreateExistingArchiveFolder();
        var onlineArchiveFile = await CreateArchiveFileWithHashAsync(
            Path.Combine(existingArchiveFolder, "existing.part1.rar"),
            "online"
        );
        var offlineArchiveFile = await CreateArchiveFileWithHashAsync(
            Path.Combine(existingArchiveFolder, "existing.part2.rar"),
            "offline"
        );
        var existingArchive = CreateExistingArchive(
            archiveConfigId: upload.UploadConfig.ArchiveConfigId,
            archiveFolderPath: existingArchiveFolder,
            archiveFiles: [onlineArchiveFile, offlineArchiveFile]
        );
        var previousUpload = CreatePreviousUpload(
            uploadConfigId: upload.UploadConfigId,
            archive: existingArchive,
            uploadState: UploadState.Completed,
            (onlineArchiveFile, OnlineState.Online),
            (offlineArchiveFile, OnlineState.Offline)
        );
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == upload.Id);
        var archiveFiles = await DbContext
            .ArchiveFiles.Where(f => f.ArchiveId == existingArchive.Id)
            .ToListAsync();

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        result.UploadedFiles.ShouldBeEmpty();
        (await File.ReadAllBytesAsync(onlineArchiveFile.FullFileName)).ShouldBe([
            .. "online"u8.ToArray(),
            (byte)0,
        ]);
        (await File.ReadAllBytesAsync(offlineArchiveFile.FullFileName)).ShouldBe([
            .. "offline"u8.ToArray(),
            (byte)0,
        ]);
        foreach (var archiveFile in archiveFiles)
        {
            archiveFile.Md5Hash.ShouldBe(ComputeMd5Hash(archiveFile.FullFileName));
        }
    }

    [Test]
    public async Task ProcessAsync_HashChangeNeededButAllFilesStillOnline_AssignsArchiveWithoutChangingFiles()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = CreateExistingArchiveFolder();
        var firstArchiveFile = await CreateArchiveFileWithHashAsync(
            Path.Combine(existingArchiveFolder, "existing.part1.rar"),
            "first"
        );
        var secondArchiveFile = await CreateArchiveFileWithHashAsync(
            Path.Combine(existingArchiveFolder, "existing.part2.rar"),
            "second"
        );
        var existingArchive = CreateExistingArchive(
            archiveConfigId: upload.UploadConfig.ArchiveConfigId,
            archiveFolderPath: existingArchiveFolder,
            archiveFiles: [firstArchiveFile, secondArchiveFile]
        );
        var previousUpload = CreatePreviousUpload(
            uploadConfigId: upload.UploadConfigId,
            archive: existingArchive,
            uploadState: UploadState.Completed,
            (firstArchiveFile, OnlineState.Online),
            (secondArchiveFile, OnlineState.Online)
        );
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == upload.Id);

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        result.UploadedFiles.Count.ShouldBe(2);
        (await File.ReadAllTextAsync(firstArchiveFile.FullFileName)).ShouldBe("first");
        (await File.ReadAllTextAsync(secondArchiveFile.FullFileName)).ShouldBe("second");
        progressTracker.PlannedFilesPerIdentifier.ShouldBeEmpty();
        archiverMock.Verify(
            a =>
                a.ArchiveAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ProcessAsync_FileToUploadIsMissingOnDisk_MarksArchiveMissingFilesWithoutChangingHashes()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = CreateExistingArchiveFolder();
        var onlineArchiveFile = await CreateArchiveFileWithHashAsync(
            Path.Combine(existingArchiveFolder, "existing.part1.rar"),
            "online"
        );
        var offlineArchiveFile = await CreateArchiveFileWithHashAsync(
            Path.Combine(existingArchiveFolder, "existing.part2.rar"),
            "offline"
        );
        var missingArchiveFile = new ArchiveFile
        {
            FullFileName = Path.Combine(existingArchiveFolder, "existing.part3.rar"),
            Md5Hash = PreviouslyComputedMd5Hash,
        };
        var onlineFileHash = onlineArchiveFile.Md5Hash;
        var offlineFileHash = offlineArchiveFile.Md5Hash;
        var existingArchive = CreateExistingArchive(
            archiveConfigId: upload.UploadConfig.ArchiveConfigId,
            archiveFolderPath: existingArchiveFolder,
            archiveFiles: [onlineArchiveFile, offlineArchiveFile, missingArchiveFile]
        );
        var previousUpload = CreatePreviousUpload(
            uploadConfigId: upload.UploadConfigId,
            archive: existingArchive,
            uploadState: UploadState.Completed,
            (onlineArchiveFile, OnlineState.Online),
            (offlineArchiveFile, OnlineState.Offline),
            (missingArchiveFile, OnlineState.Offline)
        );
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var archive = await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .SingleAsync(a => a.Id == existingArchive.Id);
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == upload.Id);
        var notification = await DbContext.Notifications.SingleAsync(n => n.UploadId == upload.Id);
        var archiveFilesById = archive.ArchiveFiles.ToDictionary(f => f.Id);

        archive.ArchiveState.ShouldBe(ArchiveState.MissingFiles);
        result.ArchiveId.ShouldBeNull();
        result.UploadState.ShouldBe(UploadState.WaitingForArchive);
        result.UploadedFiles.ShouldBeEmpty();
        (await File.ReadAllTextAsync(onlineArchiveFile.FullFileName)).ShouldBe("online");
        (await File.ReadAllTextAsync(offlineArchiveFile.FullFileName)).ShouldBe("offline");
        archiveFilesById[onlineArchiveFile.Id].Md5Hash.ShouldBe(onlineFileHash);
        archiveFilesById[offlineArchiveFile.Id].Md5Hash.ShouldBe(offlineFileHash);
        archiveFilesById[missingArchiveFile.Id].Md5Hash.ShouldBe(PreviouslyComputedMd5Hash);
        notification.NotificationKind.ShouldBe(NotificationKind.ArchiveFilesMissing);
        notification.Message.ShouldBe(
            "The archive assigned upload has missing files. Bearcat will restore them from an online mirror or repackage the release."
        );
        progressTracker.PlannedFilesPerIdentifier.ShouldBeEmpty();
        archiverMock.Verify(
            a =>
                a.ArchiveAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ProcessAsync_FileToUploadIsMissingWhileArchiveIsActivelyUploading_KeepsArchiveUnchanged()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = CreateExistingArchiveFolder();
        var missingArchiveFile = new ArchiveFile
        {
            FullFileName = Path.Combine(existingArchiveFolder, "existing.part1.rar"),
            Md5Hash = PreviouslyComputedMd5Hash,
        };
        var existingArchive = CreateExistingArchive(
            archiveConfigId: upload.UploadConfig.ArchiveConfigId,
            archiveFolderPath: existingArchiveFolder,
            archiveFiles: [missingArchiveFile]
        );
        var activeUpload = CreatePreviousUpload(
            uploadConfigId: upload.UploadConfigId,
            archive: existingArchive,
            uploadState: UploadState.Uploading
        );
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(activeUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var archive = await DbContext.Archives.SingleAsync(a => a.Id == existingArchive.Id);
        var result = await DbContext.Uploads.SingleAsync(u => u.Id == upload.Id);

        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        result.ArchiveId.ShouldBeNull();
        result.UploadState.ShouldBe(UploadState.WaitingForArchive);
        (await DbContext.Notifications.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_CarriedOverFileIsMissingOnDisk_AssignsArchive()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = CreateExistingArchiveFolder();
        var missingOnlineArchiveFile = new ArchiveFile
        {
            FullFileName = Path.Combine(existingArchiveFolder, "existing.part1.rar"),
            Md5Hash = PreviouslyComputedMd5Hash,
        };
        var offlineArchiveFile = await CreateArchiveFileWithHashAsync(
            Path.Combine(existingArchiveFolder, "existing.part2.rar"),
            "offline"
        );
        var existingArchive = CreateExistingArchive(
            archiveConfigId: upload.UploadConfig.ArchiveConfigId,
            archiveFolderPath: existingArchiveFolder,
            archiveFiles: [missingOnlineArchiveFile, offlineArchiveFile]
        );
        var previousUpload = CreatePreviousUpload(
            uploadConfigId: upload.UploadConfigId,
            archive: existingArchive,
            uploadState: UploadState.Completed,
            (missingOnlineArchiveFile, OnlineState.Online),
            (offlineArchiveFile, OnlineState.Offline)
        );
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var archive = await DbContext.Archives.SingleAsync(a => a.Id == existingArchive.Id);
        var result = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == upload.Id);

        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        result
            .UploadedFiles.ShouldHaveSingleItem()
            .ArchiveFileId.ShouldBe(missingOnlineArchiveFile.Id);
        (await File.ReadAllBytesAsync(offlineArchiveFile.FullFileName)).ShouldBe([
            .. "offline"u8.ToArray(),
            (byte)0,
        ]);
        (await DbContext.Notifications.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_CarriedOverFileHasNoHashAndNoHashChangeIsNeeded_ChangesOnlyHashOfThatFile()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchiveFolder = CreateExistingArchiveFolder();
        var onlineArchiveFileWithoutHash = await CreateArchiveFileWithHashAsync(
            Path.Combine(existingArchiveFolder, "existing.part1.rar"),
            "online"
        );
        onlineArchiveFileWithoutHash.Md5Hash = null;
        var offlineArchiveFile = await CreateArchiveFileWithHashAsync(
            Path.Combine(existingArchiveFolder, "existing.part2.rar"),
            "offline"
        );
        var offlineFileHash = offlineArchiveFile.Md5Hash;
        var existingArchive = CreateExistingArchive(
            archiveConfigId: upload.UploadConfig.ArchiveConfigId,
            archiveFolderPath: existingArchiveFolder,
            archiveFiles: [onlineArchiveFileWithoutHash, offlineArchiveFile]
        );
        var previousUpload = CreatePreviousUpload(
            uploadConfigId: upload.UploadConfigId,
            archive: existingArchive,
            uploadState: UploadState.Failed,
            (onlineArchiveFileWithoutHash, OnlineState.Online),
            (offlineArchiveFile, OnlineState.Offline)
        );
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.SingleAsync(u => u.Id == upload.Id);
        var archiveFilesById = await DbContext.ArchiveFiles.ToDictionaryAsync(f => f.Id);
        var changedOnlineFileBytes = await File.ReadAllBytesAsync(
            onlineArchiveFileWithoutHash.FullFileName
        );

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        changedOnlineFileBytes.ShouldBe([.. "online"u8.ToArray(), (byte)0]);
        archiveFilesById[onlineArchiveFileWithoutHash.Id]
            .Md5Hash.ShouldBe(Convert.ToHexString(MD5.HashData(changedOnlineFileBytes)));
        (await File.ReadAllTextAsync(offlineArchiveFile.FullFileName)).ShouldBe("offline");
        archiveFilesById[offlineArchiveFile.Id].Md5Hash.ShouldBe(offlineFileHash);
    }

    [Test]
    public async Task ProcessAsync_ArchiverCannotChangeHashInPlaceAndAllFilesStillOnline_ReusesExistingArchive()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        upload.UploadConfig.ArchiveConfig.ArchiverName = "7Zip";
        archiverFactoryMock.Setup(f => f.GetByName("7Zip")).Returns(archiverMock.Object);
        archiverMock.SetupGet(a => a.CanChangeHashInPlace).Returns(false);
        var existingArchiveFolder = CreateExistingArchiveFolder();
        var firstArchiveFile = await CreateArchiveFileWithHashAsync(
            Path.Combine(existingArchiveFolder, "existing.7z.001"),
            "first"
        );
        var secondArchiveFile = await CreateArchiveFileWithHashAsync(
            Path.Combine(existingArchiveFolder, "existing.7z.002"),
            "second"
        );
        var existingArchive = CreateExistingArchive(
            archiveConfigId: upload.UploadConfig.ArchiveConfigId,
            archiveFolderPath: existingArchiveFolder,
            archiveFiles: [firstArchiveFile, secondArchiveFile]
        );
        var previousUpload = CreatePreviousUpload(
            uploadConfigId: upload.UploadConfigId,
            archive: existingArchive,
            uploadState: UploadState.Completed,
            (firstArchiveFile, OnlineState.Online),
            (secondArchiveFile, OnlineState.Online)
        );
        DbContext.Archives.Add(existingArchive);
        DbContext.Uploads.Add(previousUpload);
        await DbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.Uploads.SingleAsync(u => u.Id == upload.Id);

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        (await DbContext.Archives.CountAsync()).ShouldBe(1);
        (await File.ReadAllTextAsync(firstArchiveFile.FullFileName)).ShouldBe("first");
        (await File.ReadAllTextAsync(secondArchiveFile.FullFileName)).ShouldBe("second");
        archiverMock.Verify(
            a =>
                a.ArchiveAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    private string CreateExistingArchiveFolder()
    {
        return Directory.CreateDirectory(Path.Combine(archiveFilesBasePath, "existing")).FullName;
    }

    private static async Task<ArchiveFile> CreateArchiveFileWithHashAsync(
        string filePath,
        string content
    )
    {
        await File.WriteAllTextAsync(filePath, content);

        return new ArchiveFile { FullFileName = filePath, Md5Hash = ComputeMd5Hash(filePath) };
    }

    private static Archive CreateExistingArchive(
        int archiveConfigId,
        string archiveFolderPath,
        List<ArchiveFile> archiveFiles
    )
    {
        return new Archive
        {
            ArchiveConfigId = archiveConfigId,
            ArchiveFolderPath = archiveFolderPath,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = archiveFiles,
            Uploads = [],
            ErrorMessages = [],
        };
    }

    private static Upload CreatePreviousUpload(
        int uploadConfigId,
        Archive archive,
        UploadState uploadState,
        params (ArchiveFile ArchiveFile, OnlineState OnlineState)[] uploadedFiles
    )
    {
        return new Upload
        {
            UploadConfigId = uploadConfigId,
            Archive = archive,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            UploadState = uploadState,
            OnlineState = OnlineState.PartiallyOnline,
            ErrorMessages = [],
            UploadedFiles = uploadedFiles
                .Select(uploadedFile => new UploadedFile
                {
                    ArchiveFile = uploadedFile.ArchiveFile,
                    HosterFileLink =
                        $"https://hoster.example/{Path.GetFileName(uploadedFile.ArchiveFile.FullFileName)}",
                    OnlineState = uploadedFile.OnlineState,
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    CheckedAt = DateTime.UtcNow.AddHours(-1),
                })
                .ToList(),
        };
    }

    private void SetupArchiverRequestingUserCancellationWhilePacking()
    {
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.IsAny<string>(),
                    "bearcat-release",
                    It.IsAny<int>(),
                    "secret",
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (
                    string _,
                    string destinationPath,
                    string _,
                    int _,
                    string? _,
                    ArchiveOptions _,
                    CancellationToken cancellationToken
                ) =>
                {
                    await File.WriteAllTextAsync(
                        Path.Combine(destinationPath, "bearcat-release.part1.rar"),
                        "partial",
                        CancellationToken.None
                    );
                    await RequestUserCancellationOfArchiveInFolderAsync(destinationPath);
                    cancellationToken.ThrowIfCancellationRequested();
                    return new ArchiveResult(true, [], null);
                }
            );
    }

    private async Task RequestUserCancellationOfArchiveInFolderAsync(string archiveFolderPath)
    {
        var archiveId = await DbContext
            .Archives.Where(a => a.ArchiveFolderPath == archiveFolderPath)
            .Select(a => a.Id)
            .SingleAsync();

        cancellationRegistry
            .RequestCancellation(new TransferIdentifier(TransferType.ArchiveCreation, archiveId))
            .ShouldBeTrue();
        userCanceledArchiveId = archiveId;
    }

    private async Task AssertArchiveWasDeletedAndUploadWasCanceledAsync(int uploadId)
    {
        DbContext.ChangeTracker.Clear();
        var archiveExists = await DbContext.Archives.AnyAsync();
        var archiveFileExists = await DbContext.ArchiveFiles.AnyAsync();
        var upload = await DbContext.Uploads.SingleAsync(u => u.Id == uploadId);
        var notification = await DbContext.Notifications.SingleAsync(n => n.UploadId == uploadId);

        archiveExists.ShouldBeFalse();
        archiveFileExists.ShouldBeFalse();
        Directory.GetFileSystemEntries(archiveFilesBasePath).ShouldBeEmpty();
        upload.ArchiveId.ShouldBeNull();
        upload.UploadState.ShouldBe(UploadState.Canceled);
        notification.NotificationKind.ShouldBe(NotificationKind.UploadCanceled);
        cancellationRegistry
            .RequestCancellation(
                new TransferIdentifier(TransferType.ArchiveCreation, userCanceledArchiveId!.Value)
            )
            .ShouldBeFalse();
    }

    private static string ComputeMd5Hash(string filePath)
    {
        return Convert.ToHexString(MD5.HashData(File.ReadAllBytes(filePath)));
    }

    private async Task CreateReleaseFolderEntriesLeftBehindByCrashAsync()
    {
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "movie.mkv"), "movie");
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "__nonce.txt"), "nonce");
        await File.WriteAllTextAsync(Path.Combine(releaseFolderPath, "Mirror.txt"), "mirror");
        var extrasFolderPath = Directory
            .CreateDirectory(Path.Combine(releaseFolderPath, "Extras", "Nested"))
            .FullName;
        await File.WriteAllTextAsync(Path.Combine(extrasFolderPath, "info.txt"), "info");
    }

    private static Mock<IFileSystemService> CreateFileSystemServiceMockDelegatingToRealFileSystem()
    {
        var realFileSystemService = new FileSystemService();
        var fileSystemServiceMock = new Mock<IFileSystemService>(MockBehavior.Strict);
        fileSystemServiceMock
            .Setup(f => f.CreateTempDirectory(It.IsAny<string>()))
            .Returns((string basePath) => realFileSystemService.CreateTempDirectory(basePath));
        fileSystemServiceMock
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns((string filePath) => realFileSystemService.FileExists(filePath));
        fileSystemServiceMock
            .Setup(f => f.DirectoryExists(It.IsAny<string>()))
            .Returns((string path) => realFileSystemService.DirectoryExists(path));
        fileSystemServiceMock
            .Setup(f => f.GetFilesInPath(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(
                (string path, bool recursive) =>
                    realFileSystemService.GetFilesInPath(path, recursive)
            );
        fileSystemServiceMock
            .Setup(f => f.GetFoldersInPath(It.IsAny<string>()))
            .Returns((string path) => realFileSystemService.GetFoldersInPath(path));
        fileSystemServiceMock
            .Setup(f => f.DeleteFileIfExists(It.IsAny<string>()))
            .Callback((string filePath) => realFileSystemService.DeleteFileIfExists(filePath));
        fileSystemServiceMock
            .Setup(f => f.DeleteDirectoryIfExists(It.IsAny<string>()))
            .Callback((string path) => realFileSystemService.DeleteDirectoryIfExists(path));

        return fileSystemServiceMock;
    }

    private void SetupArchiver(Func<Task> onArchive, ArchiveResult archiveResult)
    {
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.IsAny<string>(),
                    "bearcat-release",
                    It.IsAny<int>(),
                    "secret",
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(async () =>
            {
                await onArchive();
                return archiveResult;
            });
    }

    private void SetupArchiverWritingVolumes(params (string FileName, string Content)[] volumes)
    {
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.IsAny<string>(),
                    "bearcat-release",
                    It.IsAny<int>(),
                    "secret",
                    It.IsAny<ArchiveOptions>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (
                    string _,
                    string destinationPath,
                    string _,
                    int _,
                    string? _,
                    ArchiveOptions _,
                    CancellationToken cancellationToken
                ) =>
                {
                    releaseFolderBytesWhilePacking = Directory
                        .GetFiles(releaseFolderPath, "*", SearchOption.AllDirectories)
                        .Sum(filePath => new FileInfo(filePath).Length);
                    var createdFileNames = new List<string>();

                    foreach (var (fileName, content) in volumes)
                    {
                        var filePath = Path.Combine(destinationPath, fileName);
                        await File.WriteAllTextAsync(filePath, content, cancellationToken);
                        createdFileNames.Add(filePath);
                    }

                    return new ArchiveResult(true, createdFileNames, null);
                }
            );
    }

    private void SetupArchiveRepackagingStrategy(string strategy)
    {
        configurationProviderMock
            .Setup(p =>
                p.GetValue<ArchiveRepackagingConfiguration>(
                    It.IsAny<Expression<Func<ArchiveRepackagingConfiguration, string?>>>()
                )
            )
            .Returns(strategy);
    }

    private async Task AddPreviousArchiveAsync(
        int archiveConfigId,
        int archiveFileSizeMb,
        string? md5Hash
    )
    {
        DbContext.Archives.Add(
            new Archive
            {
                ArchiveConfigId = archiveConfigId,
                ArchiveFolderPath = Directory
                    .CreateDirectory(Path.Combine(archiveFilesBasePath, "previous"))
                    .FullName,
                ArchiveState = ArchiveState.MissingFiles,
                ArchiveFileSizeMb = archiveFileSizeMb,
                CreatedAt = DateTime.UtcNow,
                ArchiveFiles =
                [
                    new ArchiveFile { FullFileName = "previous.part1.rar", Md5Hash = md5Hash },
                ],
                Uploads = [],
                ErrorMessages = [],
            }
        );
        await DbContext.SaveChangesAsync();
    }

    private List<string> GetRelativePathsInReleaseFolder()
    {
        return Directory
            .GetFileSystemEntries(releaseFolderPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(releaseFolderPath, path))
            .ToList();
    }

    private async Task<List<string>> LoadPersistedReleaseFolderEntriesCopiedForPackingAsync()
    {
        await using var readDbContext = Database.CreateDbContext();
        var archive = await readDbContext.Archives.SingleAsync();

        return archive.ReleaseFolderEntriesCopiedForPacking;
    }

    private static AdditionalArchiveContent CreatePathContent(string name, string sourcePath)
    {
        return new AdditionalArchiveContent
        {
            Name = name,
            Type = AdditionalArchiveContentType.Path,
            SourcePath = sourcePath,
        };
    }

    private static AdditionalArchiveContent CreateTextFileContent(
        string name,
        string fileName,
        string textContent
    )
    {
        return new AdditionalArchiveContent
        {
            Name = name,
            Type = AdditionalArchiveContentType.TextFile,
            FileName = fileName,
            TextContent = textContent,
        };
    }

    private async Task<Upload> AddUploadWaitingForArchiveAsync(
        List<AdditionalArchiveContent>? additionalArchiveContents = null
    )
    {
        var uploadConfig = await AddUploadConfigAsync(
            await AddArchiveConfigAsync(additionalArchiveContents)
        );
        var upload = new Upload
        {
            UploadConfigId = uploadConfig.Id,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.WaitingForArchive,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
        };

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();

        return upload;
    }

    private async Task<UploadConfig> AddUploadConfigAsync(
        ArchiveConfig? archiveConfig = null,
        string hosterClassName = "TestHoster",
        string name = "Default upload"
    )
    {
        archiveConfig ??= await AddArchiveConfigAsync();
        var hosterRegistration = new HosterRegistration
        {
            Name = hosterClassName,
            SerializedConfig = "{}",
            HosterClassName = hosterClassName,
            IsActive = true,
        };
        var uploadConfig = new UploadConfig
        {
            ReleaseId = archiveConfig.ReleaseId,
            ArchiveConfigId = archiveConfig.Id,
            HosterRegistration = hosterRegistration,
            Name = name,
        };

        DbContext.UploadConfigs.Add(uploadConfig);
        await DbContext.SaveChangesAsync();

        return uploadConfig;
    }

    private async Task<ArchiveConfig> AddArchiveConfigAsync(
        List<AdditionalArchiveContent>? additionalArchiveContents = null
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Managed releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var release = new Release
        {
            Name = "Bearcat.Release.001",
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = releaseFolderPath,
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
            AdditionalArchiveContents = additionalArchiveContents ?? [],
        };

        DbContext.ArchiveConfigs.Add(archiveConfig);
        await DbContext.SaveChangesAsync();

        return archiveConfig;
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
