using System.Linq.Expressions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives;
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

        archiverMock = new Mock<IArchiver>(MockBehavior.Strict);
        archiverMock.SetupGet(a => a.Name).Returns("zip");
        archiverMock.SetupGet(a => a.FileExtension).Returns(".zip");
        archiverMock.SetupGet(a => a.CanChangeHashInPlace).Returns(true);

        archiverFactoryMock = new Mock<IArchiverFactory>(MockBehavior.Strict);
        archiverFactoryMock.Setup(f => f.GetByName("zip")).Returns(archiverMock.Object);
        configurationProviderMock = new Mock<IApplicationConfigurationProvider>(MockBehavior.Strict);
        configurationProviderMock
            .Setup(p =>
                p.GetValue<ArchiveRepackagingConfiguration>(
                    It.IsAny<Expression<Func<ArchiveRepackagingConfiguration, string?>>>()
                )
            )
            .Returns(ArchiveRepackagingStrategies.IncrementArchiveFileSize);

        service = new ArchiveCreationService(
            new ArchiveCreationRepository(dbContext),
            Mock.Of<ILogger<ArchiveCreationService>>(),
            archiverFactoryMock.Object,
            new FileSystemService(),
            CreateTimeProvider(),
            new NotificationService(new NotificationRepository(dbContext), CreateTimeProvider()),
            configurationProviderMock.Object
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

        result.ArchiveId.ShouldBe(existingArchive.Id);
        result.UploadState.ShouldBe(UploadState.Pending);
        ((long)changedArchiveBytes.Length).ShouldBe(originalLength + 1);
        changedArchiveBytes.Last().ShouldBe((byte)0);
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
        File.Exists(Path.Combine(releaseFolderPath, "__nonce.txt")).ShouldBeTrue();
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
    public async Task ProcessAsync_IncrementArchiveFileSizeStrategy_UsesLastArchiveFileSizePlusOne()
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
                ArchiveFiles = [],
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
        result.Notifications.Single().NotificationType.ShouldBe(NotificationType.Error);
        result
            .Notifications.Single()
            .Message.ShouldBe("Failed to create archive: Could not create archive");
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

    private async Task<Upload> AddUploadWaitingForArchiveAsync()
    {
        var uploadConfig = await AddUploadConfigAsync();
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
            LinksDistributedTo = [],
        };

        dbContext.UploadConfigs.Add(uploadConfig);
        await dbContext.SaveChangesAsync();

        return uploadConfig;
    }

    private async Task<ArchiveConfig> AddArchiveConfigAsync()
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
