using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageUploads;

public class MissingFileValidationServiceTest : BearcatIntegrationTest
{
    private const string HosterClassName = "TestHoster";

    private BearcatDbContext dbContext = null!;
    private string tempRootPath = null!;
    private MissingFileValidationService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");

        var notificationService = new NotificationService(
            new NotificationRepository(dbContext),
            CreateTimeProvider()
        );

        service = new MissingFileValidationService(
            new UploadFilesRepository(dbContext, dbContext, NoOpSecretProtector.Instance),
            new FileSystemService(),
            Mock.Of<ILogger<MissingFileValidationService>>(),
            notificationService
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
    public async Task GetUploadsWithMissingFilesAsync_ArchiveFilesMissingOnDisk_ResetsUploadAndDeletesUploadedFiles()
    {
        // Arrange
        var missingArchiveFilePath = Path.Combine(tempRootPath, "missing", "archive.part1.rar");
        var upload = await AddPendingUploadWithArchiveAsync(missingArchiveFilePath);

        dbContext.ChangeTracker.Clear();

        var trackedUpload = await dbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.UploadConfig)
                .ThenInclude(uc => uc.Release)
            .Include(u => u.Archive)
                .ThenInclude(a => a!.ArchiveFiles)
            .SingleAsync(u => u.Id == upload.Id);

        // Act
        var uploadsToSkip = await service.GetUploadsWithMissingFilesAsync(
            [trackedUpload],
            CancellationToken.None
        );

        // Assert
        uploadsToSkip.ShouldHaveSingleItem().Id.ShouldBe(upload.Id);

        dbContext.ChangeTracker.Clear();
        var result = await dbContext
            .Uploads.Include(u => u.UploadedFiles)
            .Include(u => u.Archive)
            .SingleAsync(u => u.Id == upload.Id);

        result.ArchiveId.ShouldBeNull();
        result.UploadState.ShouldBe(UploadState.WaitingForArchive);
        result.UploadedFiles.ShouldBeEmpty();
        result.Archive.ShouldBeNull();

        var archiveState = await dbContext
            .Archives.Where(a => a.ArchiveConfigId == upload.UploadConfig.ArchiveConfigId)
            .Select(a => a.ArchiveState)
            .SingleAsync();
        archiveState.ShouldBe(ArchiveState.MissingFiles);
    }

    private async Task<Upload> AddPendingUploadWithArchiveAsync(string archiveFilePath)
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
            ReleaseFolderPath = Path.Combine(tempRootPath, "release"),
            ReleaseGroup = releaseGroup,
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = Path.Combine(tempRootPath, "archives"),
            ArchiverName = "RAR",
            ArchiveNamePrefix = "bearcat-release",
            ArchivePassword = "secret",
            ArchiveFileSizeMb = 512,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = "Hoster",
            SerializedConfig = "{}",
            HosterClassName = HosterClassName,
            IsActive = true,
        };
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            Name = "Default upload",
        };
        var archiveFile = new ArchiveFile { FullFileName = archiveFilePath };
        var archive = new Archive
        {
            ArchiveConfig = archiveConfig,
            ArchiveFolderPath = Path.Combine(tempRootPath, "archives"),
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow,
            ArchiveFiles = [archiveFile],
            Uploads = [],
            ErrorMessages = [],
        };
        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            Archive = archive,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.Pending,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
            UploadedFiles =
            [
                new UploadedFile
                {
                    ArchiveFile = archiveFile,
                    HosterFileLink = "https://hoster.test/dead-link",
                    ErrorMessages = [],
                    OnlineState = OnlineState.Online,
                    CreatedAt = DateTime.UtcNow,
                    CheckedAt = DateTime.UtcNow,
                },
            ],
        };

        dbContext.Uploads.Add(upload);
        await dbContext.SaveChangesAsync();

        return upload;
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
