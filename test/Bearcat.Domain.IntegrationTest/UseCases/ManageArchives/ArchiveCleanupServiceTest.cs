using Bearcat.Abstractions;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.UseCases.ManageArchives;
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

public class ArchiveCleanupServiceTest : BearcatIntegrationTest
{
    private const string MirrorHosterClassName = "MirrorHoster";

    private BearcatDbContext dbContext = null!;
    private string releaseFolderPath = null!;
    private string archiveFilesBasePath = null!;
    private string tempRootPath = null!;
    private Mock<IApplicationConfigurationProvider> configurationMock = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;
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

        configurationMock = new Mock<IApplicationConfigurationProvider>(MockBehavior.Strict);
        hosterFactoryMock = new Mock<IHosterFactory>(MockBehavior.Strict);
        hosterFactoryMock
            .Setup(f => f.GetByName(MirrorHosterClassName))
            .Returns(Mock.Of<IHosterWithDownload>());

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
    public async Task ProcessAsync_AutoDeleteDisabled_KeepsArchive()
    {
        // Arrange
        var archive = await AddScenarioAsync();
        SetupConfiguration(autoDeleteArchives: false);

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
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
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

    private void SetupConfiguration(bool autoDeleteArchives = true, int archiveRetentionDays = 30)
    {
        configurationMock
            .Setup(c => c.GetValue<ArchiveCleanupConfiguration>(a => a.AutoDeleteArchives))
            .Returns(autoDeleteArchives);
        configurationMock
            .Setup(c => c.GetValue<ArchiveCleanupConfiguration>(a => a.ArchiveRetentionDays))
            .Returns(archiveRetentionDays);
    }

    private async Task ShouldBeDeletedAsync(Archive archive)
    {
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Archives.Include(a => a.ArchiveFiles)
            .SingleAsync(a => a.Id == archive.Id);

        result.ArchiveState.ShouldBe(ArchiveState.Deleted);
        result.ArchiveFiles.ShouldAllBe(f => !File.Exists(f.FullFileName));
        Directory.Exists(archive.ArchiveFolderPath).ShouldBeFalse();
    }

    private async Task ShouldStillBeCreatedAsync(Archive archive)
    {
        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Archives.Include(a => a.ArchiveFiles)
            .SingleAsync(a => a.Id == archive.Id);

        result.ArchiveState.ShouldBe(ArchiveState.Created);
        result.ArchiveFiles.ShouldAllBe(f => File.Exists(f.FullFileName));
        Directory.Exists(archive.ArchiveFolderPath).ShouldBeTrue();
    }

    private ArchiveCleanupService CreateService(IFileSystemService fileSystemService)
    {
        return new ArchiveCleanupService(
            new ArchiveCleanupRepository(dbContext),
            configurationMock.Object,
            new MirrorCoverageEvaluator(hosterFactoryMock.Object),
            new LocalArchiveDeleter(fileSystemService),
            CreateTimeProvider(),
            Mock.Of<ILogger<ArchiveCleanupService>>()
        );
    }

    private async Task<Archive> AddScenarioAsync(
        ReleaseType releaseType = ReleaseType.Managed,
        DateTime? uploadedAt = null,
        bool mirrorDownloadsEnabled = false,
        bool excludeFromAutoCleanup = false
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

        dbContext.UploadConfigs.Add(uploadConfig);
        await dbContext.SaveChangesAsync();

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
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow.AddDays(-90),
            ArchiveFiles = archiveFiles,
            Uploads = [],
            ErrorMessages = [],
        };

        dbContext.Archives.Add(archive);
        await dbContext.SaveChangesAsync();

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
        var uploadConfig = await dbContext.UploadConfigs.FirstAsync(c =>
            c.ArchiveConfigId == archive.ArchiveConfigId
        );
        var archiveFiles = await dbContext
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

        dbContext.Uploads.Add(upload);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
