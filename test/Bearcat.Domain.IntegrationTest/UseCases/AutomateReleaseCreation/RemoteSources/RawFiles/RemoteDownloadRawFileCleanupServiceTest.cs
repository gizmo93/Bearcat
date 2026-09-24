using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.RawFiles;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.AutomateReleaseCreation.RemoteSources.RawFiles;

public class RemoteDownloadRawFileCleanupServiceTest : BearcatIntegrationTest
{
    private const string MirrorHosterClassName = "MirrorHoster";
    private const string ReleaseName = "Show.S01E01.German.1080p.WEB.x264-GRP";

    private static readonly DateTime StartTime = new(
        2026,
        9,
        23,
        12,
        0,
        0,
        DateTimeKind.Unspecified
    );

    private BearcatDbContext dbContext = null!;
    private string tempRootPath = null!;
    private string targetPath = null!;
    private string rawFolderPath = null!;
    private string archiveFilesBasePath = null!;
    private RemoteDownloadRawFileCleanupService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        tempRootPath = Path.Combine(Path.GetTempPath(), $"bearcat-tests-{Guid.NewGuid():N}");
        targetPath = Directory.CreateDirectory(Path.Combine(tempRootPath, "downloads")).FullName;
        rawFolderPath = Directory.CreateDirectory(Path.Combine(targetPath, ReleaseName)).FullName;
        File.WriteAllText(Path.Combine(rawFolderPath, "release.mkv"), "raw");
        archiveFilesBasePath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "archives"))
            .FullName;

        var hosterFactoryMock = new Mock<IHosterFactory>(MockBehavior.Strict);
        hosterFactoryMock
            .Setup(f => f.GetByName(MirrorHosterClassName))
            .Returns(Mock.Of<IHosterWithDownload>());

        service = new RemoteDownloadRawFileCleanupService(
            new RemoteSourceDownloadRepository(dbContext),
            new UnmanagedReleaseConverter(new MirrorCoverageEvaluator(hosterFactoryMock.Object)),
            new RemoteDownloadFolderService(
                new FileSystemService(),
                NullLogger<RemoteDownloadFolderService>.Instance
            ),
            new NotificationService(
                repository: new NotificationRepository(dbContext),
                timeProvider: CreateTimeProvider(),
                configurationProvider: CreateNotificationConfigurationProvider()
            ),
            NullLogger<RemoteDownloadRawFileCleanupService>.Instance
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
    public async Task ProcessAsync_EveryUploadConfigCompleted_ConvertsReleaseAndDeletesRawFolder()
    {
        // Arrange
        var release = await AddScenarioAsync();

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var verificationContext = CreateDbContext();
        var result = await verificationContext.Releases.SingleAsync(r => r.Id == release.Id);
        var notification = await verificationContext.Notifications.SingleAsync(n =>
            n.ReleaseId == release.Id
        );

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        result.ReleaseFolderPath.ShouldBeNull();
        Directory.Exists(rawFolderPath).ShouldBeFalse();
        Directory
            .EnumerateFiles(archiveFilesBasePath, "*", SearchOption.AllDirectories)
            .ShouldNotBeEmpty();
        notification.NotificationKind.ShouldBe(NotificationKind.ReleaseAutoConvertedToUnmanaged);
        notification.Message.ShouldContain(rawFolderPath);
        notification.Message.ShouldContain("were deleted");
    }

    [Test]
    public async Task ProcessAsync_KeepRawFilesEnabled_LeavesReleaseUntouched()
    {
        // Arrange
        var release = await AddScenarioAsync(keepRawFiles: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldBeUntouchedAsync(release, rawFolderPath);
    }

    [Test]
    public async Task ProcessAsync_OneUploadConfigWithoutCompletedUpload_LeavesReleaseUntouched()
    {
        // Arrange
        var release = await AddScenarioAsync(withUploadConfigWithoutCompletedUpload: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldBeUntouchedAsync(release, rawFolderPath);
    }

    [Test]
    public async Task ProcessAsync_ReleaseHasActiveUpload_LeavesReleaseUntouched()
    {
        // Arrange
        var release = await AddScenarioAsync(additionalUploadState: UploadState.Uploading);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldBeUntouchedAsync(release, rawFolderPath);
    }

    [Test]
    public async Task ProcessAsync_ReleaseWithoutUploadConfigs_LeavesReleaseUntouched()
    {
        // Arrange
        var release = await AddScenarioAsync(withUploadConfigs: false);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldBeUntouchedAsync(release, rawFolderPath);
    }

    [Test]
    public async Task ProcessAsync_ReleaseFolderWasRelocated_LeavesReleaseUntouched()
    {
        // Arrange
        var relocatedFolderPath = Directory
            .CreateDirectory(Path.Combine(tempRootPath, "relocated", ReleaseName))
            .FullName;
        var release = await AddScenarioAsync(releaseFolderPath: relocatedFolderPath);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldBeUntouchedAsync(release, relocatedFolderPath);
    }

    [Test]
    public async Task ProcessAsync_ArchiveInsideRawFolder_ConvertsReleaseAndKeepsRawFolder()
    {
        // Arrange
        var release = await AddScenarioAsync(archiveInsideRawFolder: true);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        var verificationContext = CreateDbContext();
        var result = await verificationContext.Releases.SingleAsync(r => r.Id == release.Id);
        var notification = await verificationContext.Notifications.SingleAsync(n =>
            n.ReleaseId == release.Id
        );

        result.ReleaseType.ShouldBe(ReleaseType.Unmanaged);
        result.ReleaseFolderPath.ShouldBeNull();
        Directory.Exists(rawFolderPath).ShouldBeTrue();
        notification.NotificationKind.ShouldBe(NotificationKind.ReleaseAutoConvertedToUnmanaged);
        notification.Message.ShouldContain(rawFolderPath);
        notification.Message.ShouldContain("were kept");
    }

    [Test]
    public async Task ProcessAsync_NoCreatedArchiveAndNoMirror_LeavesReleaseUntouched()
    {
        // Arrange
        var release = await AddScenarioAsync(archiveState: ArchiveState.Deleted);

        // Act
        await service.ProcessAsync(CancellationToken.None);

        // Assert
        await ShouldBeUntouchedAsync(release, rawFolderPath);
    }

    private async Task ShouldBeUntouchedAsync(Release release, string expectedReleaseFolderPath)
    {
        var verificationContext = CreateDbContext();
        var result = await verificationContext.Releases.SingleAsync(r => r.Id == release.Id);

        result.ReleaseType.ShouldBe(ReleaseType.Managed);
        result.ReleaseFolderPath.ShouldBe(expectedReleaseFolderPath);
        Directory.Exists(rawFolderPath).ShouldBeTrue();
        (
            await verificationContext.Notifications.AnyAsync(n => n.ReleaseId == release.Id)
        ).ShouldBeFalse();
    }

    private async Task<Release> AddScenarioAsync(
        bool keepRawFiles = false,
        bool withUploadConfigs = true,
        bool withUploadConfigWithoutCompletedUpload = false,
        UploadState? additionalUploadState = null,
        string? releaseFolderPath = null,
        bool archiveInsideRawFolder = false,
        ArchiveState archiveState = ArchiveState.Created
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Releases",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };
        var automation = new RemoteSourceAutomation
        {
            Name = "Automation",
            RemoteSourceRegistration = new RemoteSourceRegistration
            {
                Name = "Main FTP",
                SerializedConfig = "{}",
                SourceClassName = "FakeRemoteSource",
                IsActive = true,
            },
            RemotePath = "/incoming",
            TargetPath = targetPath,
            ReleaseTemplate = new ReleaseTemplate
            {
                Name = "Managed template",
                ReleaseType = ReleaseType.Managed,
                ReleaseGroup = releaseGroup,
            },
            KeepRawFiles = keepRawFiles,
            IsEnabled = true,
        };
        var release = new Release
        {
            Name = ReleaseName,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = releaseFolderPath ?? rawFolderPath,
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
        dbContext.RemoteSourceAutomations.Add(automation);
        await dbContext.SaveChangesAsync();

        var archiveFolderPath = archiveInsideRawFolder
            ? Directory.CreateDirectory(Path.Combine(rawFolderPath, "archives")).FullName
            : Directory
                .CreateDirectory(Path.Combine(archiveFilesBasePath, Guid.NewGuid().ToString("N")))
                .FullName;
        var archiveFile = new ArchiveFile
        {
            FullFileName = Path.Combine(archiveFolderPath, "archive.part1.rar"),
        };
        await File.WriteAllTextAsync(archiveFile.FullFileName, "archive");
        var archive = new Archive
        {
            ArchiveConfigId = archiveConfig.Id,
            ArchiveFolderPath = archiveFolderPath,
            ArchiveState = archiveState,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ArchiveFiles = [archiveFile],
            Uploads = [],
            ErrorMessages = [],
        };

        dbContext.Archives.Add(archive);
        await dbContext.SaveChangesAsync();

        if (withUploadConfigs)
        {
            var hosterRegistration = new HosterRegistration
            {
                Name = "Mirror",
                SerializedConfig = "{}",
                HosterClassName = MirrorHosterClassName,
                IsActive = true,
                UseForMirrorDownloads = false,
            };
            var uploadConfig = AddUploadConfig(
                release,
                archiveConfig,
                hosterRegistration,
                "Default upload"
            );
            await dbContext.SaveChangesAsync();

            AddCompletedUpload(uploadConfig, archive, archiveFile);

            if (withUploadConfigWithoutCompletedUpload)
            {
                var failedUploadConfig = AddUploadConfig(
                    release,
                    archiveConfig,
                    hosterRegistration,
                    "Second upload"
                );
                await dbContext.SaveChangesAsync();

                AddUpload(failedUploadConfig, UploadState.Failed);
            }

            if (additionalUploadState is not null)
            {
                AddUpload(uploadConfig, additionalUploadState.Value);
            }
        }

        dbContext.RemoteSourceDownloads.Add(
            new RemoteSourceDownload
            {
                RemoteSourceAutomationId = automation.Id,
                RemoteSourceRegistrationId = automation.RemoteSourceRegistrationId,
                SourceName = "Main FTP",
                RemoteFolderPath = $"/incoming/{ReleaseName}",
                FolderName = ReleaseName,
                LocalFolderPath = rawFolderPath,
                ReleaseTemplateId = automation.ReleaseTemplateId,
                PrimaryLanguageCode = "de",
                KeepRawFiles = keepRawFiles,
                State = RemoteSourceDownloadState.ReleaseCreated,
                FileCount = 1,
                TotalBytes = 3,
                LastChangedAt = StartTime,
                DiscoveredAt = StartTime,
                StartedAt = StartTime,
                CompletedAt = StartTime,
                ReleaseId = release.Id,
            }
        );

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return release;
    }

    private UploadConfig AddUploadConfig(
        Release release,
        ArchiveConfig archiveConfig,
        HosterRegistration hosterRegistration,
        string name
    )
    {
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            Name = name,
        };

        dbContext.UploadConfigs.Add(uploadConfig);

        return uploadConfig;
    }

    private void AddCompletedUpload(
        UploadConfig uploadConfig,
        Archive archive,
        ArchiveFile archiveFile
    )
    {
        dbContext.Uploads.Add(
            new Upload
            {
                UploadConfigId = uploadConfig.Id,
                ArchiveId = archive.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UploadedAt = DateTime.UtcNow.AddDays(-1),
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
                        CreatedAt = DateTime.UtcNow.AddDays(-1),
                        CheckedAt = DateTime.UtcNow.AddDays(-1),
                    },
                ],
            }
        );
    }

    private void AddUpload(UploadConfig uploadConfig, UploadState uploadState)
    {
        dbContext.Uploads.Add(
            new Upload
            {
                UploadConfigId = uploadConfig.Id,
                CreatedAt = DateTime.UtcNow,
                UploadState = uploadState,
                OnlineState = OnlineState.Unknown,
                ErrorMessages = [],
                UploadedFiles = [],
            }
        );
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
