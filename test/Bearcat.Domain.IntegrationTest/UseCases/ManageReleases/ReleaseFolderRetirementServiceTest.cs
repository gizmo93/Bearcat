using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class ReleaseFolderRetirementServiceTest : BearcatIntegrationTest
{
    private const string MirrorHosterClassName = "MirrorHoster";

    private BearcatDbContext dbContext = null!;
    private string tempRootPath = null!;
    private string releaseFolderPath = null!;
    private string archiveFilesBasePath = null!;
    private Mock<IApplicationConfigurationProvider> configurationMock = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;
    private ReleaseFolderRetirementService service = null!;

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

        service = new ReleaseFolderRetirementService(
            new ReleaseFolderRetirementRepository(dbContext),
            configurationMock.Object,
            new UnmanagedReleaseConverter(new MirrorCoverageEvaluator(hosterFactoryMock.Object)),
            new NotificationService(
                repository: new NotificationRepository(dbContext),
                timeProvider: CreateTimeProvider(),
                configurationProvider: CreateNotificationConfigurationProvider()
            ),
            CreateTimeProvider(),
            NullLogger<ReleaseFolderRetirementService>.Instance
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
    public async Task ProcessAsync_ReleaseIsOverdue_ConvertsAndNotifiesWithReleaseFolderPath()
    {
        // Arrange
        var release = await AddScenarioAsync();
        SetupConfiguration();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Releases.SingleAsync(r => r.Id == release.Id);
        var notification = await dbContext.Notifications.SingleAsync(n =>
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
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Releases.SingleAsync(r => r.Id == release.Id);
        var notification = await dbContext.Notifications.SingleAsync(n =>
            n.ReleaseId == release.Id
        );

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        notification.Message.ShouldContain(releaseFolderPath);
        notification.Message.ShouldContain("delete only the release data there");
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
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Releases.SingleAsync(r => r.Id == release.Id);

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
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Releases.SingleAsync(r => r.Id == release.Id);

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

    private void SetupConfiguration(
        bool autoConvertToUnmanaged = true,
        int releaseFolderRetentionDays = 14
    )
    {
        configurationMock
            .Setup(c => c.GetValue<ArchiveCleanupConfiguration>(a => a.AutoConvertToUnmanaged))
            .Returns(autoConvertToUnmanaged);
        configurationMock
            .Setup(c => c.GetValue<ArchiveCleanupConfiguration>(a => a.ReleaseFolderRetentionDays))
            .Returns(releaseFolderRetentionDays);
    }

    private async Task ShouldStillBeManagedAsync(Release release)
    {
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.Releases.SingleAsync(r => r.Id == release.Id);

        result.ReleaseType.ShouldBe(ReleaseType.Managed);
        result.ReleaseFolderPath.ShouldBe(releaseFolderPath);
        (await dbContext.Notifications.AnyAsync(n => n.ReleaseId == release.Id)).ShouldBeFalse();
    }

    private async Task<Release> AddScenarioAsync(
        DateTime? uploadedAt = null,
        DateTime? uploadsPostedAt = null,
        bool withCompletedUpload = true,
        ArchiveState archiveState = ArchiveState.Created,
        bool mirrorDownloadsEnabled = false,
        bool excludeFromAutoCleanup = false,
        bool archiveInsideReleaseFolder = false,
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

        dbContext.UploadConfigs.Add(uploadConfig);
        await dbContext.SaveChangesAsync();

        var archiveFolderPath = archiveInsideReleaseFolder
            ? Directory.CreateDirectory(Path.Combine(releaseFolderPath, "archives")).FullName
            : Directory
                .CreateDirectory(Path.Combine(archiveFilesBasePath, Guid.NewGuid().ToString("N")))
                .FullName;
        var archiveFile = new ArchiveFile
        {
            FullFileName = Path.Combine(archiveFolderPath, "archive.part1.rar"),
        };
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

        dbContext.Archives.Add(archive);
        await dbContext.SaveChangesAsync();

        if (withCompletedUpload)
        {
            dbContext.Uploads.Add(
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
            dbContext.Uploads.Add(
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

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

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
