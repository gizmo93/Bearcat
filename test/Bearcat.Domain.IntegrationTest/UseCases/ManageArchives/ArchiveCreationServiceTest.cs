using Bearcat.Abstractions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
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

        archiverFactoryMock = new Mock<IArchiverFactory>(MockBehavior.Strict);

        service = new ArchiveCreationService(
            new ArchiveCreationRepository(dbContext),
            Mock.Of<ILogger<ArchiveCreationService>>(),
            archiverFactoryMock.Object,
            new FileSystemService(),
            CreateTimeProvider(),
            new NotificationService(new NotificationRepository(dbContext), CreateTimeProvider())
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
        result.ArchiveFolderPath.ShouldStartWith(archiveFilesBasePath);
        result
            .ArchiveFiles.Select(f => f.FullFileName)
            .ShouldBe(["archive.part1.rar", "archive.part2.rar"]);
        result.Uploads.Single().Id.ShouldBe(upload.Id);
        result.Uploads.Single().UploadState.ShouldBe(UploadState.Pending);
        File.Exists(Path.Combine(releaseFolderPath, "__nonce")).ShouldBeTrue();
        archiverFactoryMock.Verify(f => f.GetByName("zip"), Times.Once);
        archiverMock.Verify(
            a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.Is<string>(p => p.StartsWith(archiveFilesBasePath)),
                    "bearcat-release",
                    512,
                    "secret",
                    CancellationToken.None
                ),
            Times.Once
        );
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

    private async Task<UploadConfig> AddUploadConfigAsync()
    {
        var archiveConfig = await AddArchiveConfigAsync();
        var hosterRegistration = new HosterRegistration
        {
            Name = "Hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        var uploadConfig = new UploadConfig
        {
            ReleaseId = archiveConfig.ReleaseId,
            ArchiveConfigId = archiveConfig.Id,
            HosterRegistration = hosterRegistration,
            Name = "Default upload",
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
