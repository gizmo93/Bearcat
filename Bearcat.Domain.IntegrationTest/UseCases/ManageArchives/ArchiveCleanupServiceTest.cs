using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageArchives;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageArchives;

public class ArchiveCleanupServiceTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private string releaseFolderPath = null!;
    private string archiveFilesBasePath = null!;
    private string tempRootPath = null!;
    private Mock<IApplicationConfigurationOverrideCache> overrideCacheMock = null!;
    private Mock<IApplicationConfigurationProvider> configurationMock = null!;
    private ArchiveCleanupService service = null!;

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

        overrideCacheMock = new Mock<IApplicationConfigurationOverrideCache>(MockBehavior.Strict);
        configurationMock = new Mock<IApplicationConfigurationProvider>(MockBehavior.Strict);

        service = CreateService(new FileSystemService());
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
    public async Task ProcessAsync_CacheIsNotInitialized_DoesNotChangeArchives()
    {
        // Arrange
        var archive = await AddUploadedArchiveAsync();
        overrideCacheMock.SetupGet(c => c.IsInitialized).Returns(false);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(archive.Id);
        result.ArchiveState.ShouldBe(ArchiveState.Created);
        Directory.Exists(archive.ArchiveFolderPath).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_AutoCleanupDisabled_DoesNotChangeArchives()
    {
        // Arrange
        var archive = await AddUploadedArchiveAsync();
        overrideCacheMock.SetupGet(c => c.IsInitialized).Returns(true);
        configurationMock
            .Setup(c => c.GetValue<ArchiveCleanupConfiguration>(a => a.AutoCleanup))
            .Returns(false);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(archive.Id);
        result.ArchiveState.ShouldBe(ArchiveState.Created);
        Directory.Exists(archive.ArchiveFolderPath).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_DeletableArchiveExists_DeletesFolderAndMarksArchiveDeleted()
    {
        // Arrange
        var archive = await AddUploadedArchiveAsync();
        overrideCacheMock.SetupGet(c => c.IsInitialized).Returns(true);
        configurationMock
            .Setup(c => c.GetValue<ArchiveCleanupConfiguration>(a => a.AutoCleanup))
            .Returns(true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(archive.Id);
        result.ArchiveState.ShouldBe(ArchiveState.Deleted);
        Directory.Exists(archive.ArchiveFolderPath).ShouldBeFalse();
    }

    [Test]
    public async Task ProcessAsync_ArchiveHasPendingUploads_DoesNotDeleteArchive()
    {
        // Arrange
        var archive = await AddArchiveAsync(uploadedAt: null);
        overrideCacheMock.SetupGet(c => c.IsInitialized).Returns(true);
        configurationMock
            .Setup(c => c.GetValue<ArchiveCleanupConfiguration>(a => a.AutoCleanup))
            .Returns(true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(archive.Id);
        result.ArchiveState.ShouldBe(ArchiveState.Created);
        Directory.Exists(archive.ArchiveFolderPath).ShouldBeTrue();
    }

    [Test]
    public async Task ProcessAsync_DeleteDirectoryFails_KeepsArchiveCreated()
    {
        // Arrange
        var archive = await AddUploadedArchiveAsync();
        var fileSystemServiceMock = new Mock<IFileSystemService>(MockBehavior.Strict);
        fileSystemServiceMock
            .Setup(f => f.DeleteDirectoryIfExists(archive.ArchiveFolderPath))
            .Throws(new IOException("Could not delete archive folder"));
        service = CreateService(fileSystemServiceMock.Object);

        overrideCacheMock.SetupGet(c => c.IsInitialized).Returns(true);
        configurationMock
            .Setup(c => c.GetValue<ArchiveCleanupConfiguration>(a => a.AutoCleanup))
            .Returns(true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Archives.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(archive.Id);
        result.ArchiveState.ShouldBe(ArchiveState.Created);
        Directory.Exists(archive.ArchiveFolderPath).ShouldBeTrue();
        fileSystemServiceMock.VerifyAll();
    }

    private ArchiveCleanupService CreateService(IFileSystemService fileSystemService)
    {
        return new ArchiveCleanupService(
            new ArchiveCleanupRepository(dbContext),
            configurationMock.Object,
            overrideCacheMock.Object,
            fileSystemService,
            Mock.Of<ILogger<ArchiveCleanupService>>()
        );
    }

    private async Task<Archive> AddUploadedArchiveAsync()
    {
        return await AddArchiveAsync(uploadedAt: DateTime.UtcNow);
    }

    private async Task<Archive> AddArchiveAsync(DateTime? uploadedAt)
    {
        var uploadConfig = await AddUploadConfigAsync();
        var archiveFolderPath = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, Guid.NewGuid().ToString("N")))
            .FullName;
        var archive = new Archive
        {
            ArchiveConfigId = uploadConfig.ArchiveConfigId,
            ArchiveFolderPath = archiveFolderPath,
            ArchiveState = ArchiveState.Created,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [],
            Uploads =
            [
                new Upload
                {
                    UploadConfigId = uploadConfig.Id,
                    CreatedAt = DateTime.UtcNow,
                    UploadedAt = uploadedAt,
                    UploadState = uploadedAt is null ? UploadState.Pending : UploadState.Completed,
                    OnlineState = OnlineState.Unknown,
                    ErrorMessages = [],
                },
            ],
            ErrorMessages = [],
        };

        dbContext.Archives.Add(archive);
        await dbContext.SaveChangesAsync();

        return archive;
    }

    private async Task<UploadConfig> AddUploadConfigAsync()
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
        var hosterRegistration = new HosterRegistration
        {
            Name = "Hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            Name = "Default upload",
            LinksDistributedTo = [],
        };

        dbContext.UploadConfigs.Add(uploadConfig);
        await dbContext.SaveChangesAsync();

        return uploadConfig;
    }
}
