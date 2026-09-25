using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
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

public class ArchiveCreationServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private string releaseFolderPath = null!;
    private string archiveFilesBasePath = null!;
    private string tempRootPath = null!;
    private Mock<IArchiver> archiverMock = null!;
    private Mock<IArchiverFactory> archiverFactoryMock = null!;
    private Mock<IApplicationConfigurationProvider> configurationProviderMock = null!;
    private string additionalContentSourcePath = null!;
    private ArchiveCreationService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
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

        service = CreateService(new FileSystemService());
    }

    private ArchiveCreationService CreateService(IFileSystemService fileSystemService)
    {
        return new ArchiveCreationService(
            new ArchiveCreationRepository(dbContext),
            Mock.Of<ILogger<ArchiveCreationService>>(),
            archiverFactoryMock.Object,
            fileSystemService,
            CreateTimeProvider(),
            new NotificationService(
                repository: new NotificationRepository(dbContext),
                timeProvider: CreateTimeProvider(),
                configurationProvider: CreateNotificationConfigurationProvider()
            ),
            configurationProviderMock.Object,
            new ReleaseFolderEntriesForPackingService(fileSystemService)
        );
    }

    [TearDown]
    public async Task DisposeResourcesAsync()
    {
        await dbContext.DisposeAsync();

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
        dbContext.Archives.Add(orphanedArchive);
        await dbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var result = await dbContext.Archives.AnyAsync();

        result.ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_AssignableArchiveExists_AssignsArchiveAndDoesNotInvokeArchiver()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        var existingArchive = new Archive
        {
            ArchiveConfigId = upload.UploadConfig.ArchiveConfigId,
            ArchiveFolderPath = Directory
                .CreateDirectory(Path.Combine(archiveFilesBasePath, "existing"))
                .FullName,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [new ArchiveFile { FullFileName = "existing.part1.rar" }],
            Uploads = [],
            ErrorMessages = [],
        };
        dbContext.Archives.Add(existingArchive);
        await dbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Uploads.SingleAsync();

        result.ShouldNotBeNull();
        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        archiverFactoryMock.Verify(f => f.GetByName(It.IsAny<string>()), Times.Never);
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
        dbContext.Archives.Add(existingArchive);
        dbContext.Uploads.Add(previousUpload);
        await dbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Uploads.SingleAsync(u => u.Id == upload.Id);
        var changedArchiveBytes = await File.ReadAllBytesAsync(archiveFilePath);
        var changedArchiveFile = await dbContext.ArchiveFiles.SingleAsync(f =>
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
            ArchiveFiles = [new ArchiveFile { FullFileName = archiveFilePath }],
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
        dbContext.Archives.Add(existingArchive);
        dbContext.Uploads.Add(previousUpload);
        await dbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Uploads.SingleAsync(u => u.Id == upload.Id);

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        new FileInfo(archiveFilePath).Length.ShouldBe(originalLength);
        archiverFactoryMock.Verify(f => f.GetByName(It.IsAny<string>()), Times.Never);
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
        dbContext.Archives.Add(existingArchive);
        dbContext.Uploads.AddRange(
            secondUpload,
            previousFirstHosterUpload,
            previousSecondHosterUpload
        );
        await dbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
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
        dbContext.Archives.Add(existingArchive);
        dbContext.Uploads.Add(previousUpload);
        await dbContext.SaveChangesAsync();

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
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new ArchiveResult(true, ["new-archive.7z.001"], null));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
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
        dbContext.Archives.Add(existingArchive);
        dbContext.Uploads.AddRange(previousUpload, activeUpload);
        await dbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Uploads.SingleAsync(u => u.Id == upload.Id);

        result.ArchiveId.ShouldBeNull();
        result.UploadState.ShouldBe(UploadState.WaitingForArchive);
        new FileInfo(archiveFilePath).Length.ShouldBe(originalLength);
        archiverFactoryMock.Verify(f => f.GetByName(It.IsAny<string>()), Times.Never);
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
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new ArchiveResult(true, ["archive.part1.rar", "archive.part2.rar"], null)
            );

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
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
                    CancellationToken.None
                ),
            Times.Once
        );
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
                    CancellationToken.None
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
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new ArchiveResult(true, ["archive.part1.rar"], null));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.Include(a => a.Uploads).SingleAsync();

        result.ArchiveFileSizeMb.ShouldBe(512);
        result.Uploads.Single().Id.ShouldBe(upload.Id);
    }

    [Test]
    public async Task ProcessAsync_IncrementArchiveFileSizeStrategyWithUnknownPreviousHashes_UsesLastArchiveFileSizePlusOne()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        dbContext.Archives.Add(
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
        await dbContext.SaveChangesAsync();

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
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new ArchiveResult(true, ["archive.part1.rar"], null));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.OrderByDescending(a => a.Id).FirstAsync();

        result.ArchiveFileSizeMb.ShouldBe(513);
    }

    [Test]
    public async Task ProcessAsync_IncrementArchiveFileSizeStrategyWithKnownPreviousHashes_PacksAtBaseArchiveFileSize()
    {
        // Arrange
        var upload = await AddUploadWaitingForArchiveAsync();
        dbContext.Archives.Add(
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
        await dbContext.SaveChangesAsync();

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
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new ArchiveResult(true, ["archive.part1.rar"], null));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.OrderByDescending(a => a.Id).FirstAsync();

        result.ArchiveFileSizeMb.ShouldBe(512);
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
        dbContext.Archives.AddRange(existingArchive, blockedHashArchive);
        dbContext.Uploads.Add(previousUpload);
        await dbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var changedArchiveBytes = await File.ReadAllBytesAsync(archiveFilePath);
        var changedArchiveFile = await dbContext.ArchiveFiles.SingleAsync(f =>
            f.FullFileName == archiveFilePath
        );

        changedArchiveBytes.Length.ShouldBe(originalBytes.Length + 2);
        changedArchiveFile.Md5Hash.ShouldBe(Convert.ToHexString(MD5.HashData(changedArchiveBytes)));
        changedArchiveFile.Md5Hash.ShouldNotBe(hashAfterFirstAppend);
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
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new ArchiveResult(false, [], ["Could not create archive"]));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
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
                    CancellationToken.None
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
        dbContext.Uploads.Add(secondUpload);
        await dbContext.SaveChangesAsync();

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
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new ArchiveResult(true, ["archive.part1.rar"], null));

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.Include(a => a.Uploads).SingleAsync();

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
                    CancellationToken.None
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
        dbContext.Archives.Add(existingArchive);
        dbContext.Uploads.Add(previousUpload);
        await dbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
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

        dbContext.Archives.Add(existingArchive);
        dbContext.Uploads.Add(olderUpload);
        await dbContext.SaveChangesAsync();

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

        dbContext.Uploads.Add(newestUpload);
        await dbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
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

        dbContext.Archives.Add(existingArchive);
        dbContext.Uploads.Add(previousUpload);
        await dbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
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
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.SingleAsync();

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
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.SingleAsync();

        Guid.TryParse(nonceDuringArchiving, out _).ShouldBeTrue();
        File.Exists(noncePath).ShouldBeFalse();
        result.ArchiveState.ShouldBe(ArchiveState.Created);
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
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
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
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.SingleAsync();

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
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.SingleAsync();

        result.ArchiveState.ShouldBe(ArchiveState.CreationFailed);
        result.ErrorMessages.ShouldBe([
            $"Cannot place additional archive content \"Missing file\" into the release folder because its source path \"{missingSourcePath}\" does not exist.",
        ]);
        result.ReleaseFolderEntriesCopiedForPacking.ShouldBeEmpty();
        GetRelativePathsInReleaseFolder().ShouldBe(["movie.mkv"]);
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
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.SingleAsync();

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
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
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
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.SingleAsync();

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
        dbContext.Archives.Add(
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
        await dbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.SingleAsync();

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
        dbContext.Archives.Add(
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
        await dbContext.SaveChangesAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var result = await dbContext.Archives.AnyAsync();

        result.ShouldBeFalse();
        GetRelativePathsInReleaseFolder().ShouldBe(["movie.mkv"]);
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

        dbContext.Uploads.Add(upload);
        await dbContext.SaveChangesAsync();

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

        dbContext.UploadConfigs.Add(uploadConfig);
        await dbContext.SaveChangesAsync();

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

        dbContext.ArchiveConfigs.Add(archiveConfig);
        await dbContext.SaveChangesAsync();

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
