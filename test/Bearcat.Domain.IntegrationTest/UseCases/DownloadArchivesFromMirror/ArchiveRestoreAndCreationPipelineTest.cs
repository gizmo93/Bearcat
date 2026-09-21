using System.Linq.Expressions;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Cancellation;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Progress;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Sources;
using Bearcat.Domain.UseCases.ManageArchives;
using Bearcat.Domain.UseCases.ManageNotifications;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.FileSystem;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.DownloadArchivesFromMirror;

public class ArchiveRestoreAndCreationPipelineTest : BearcatIntegrationTest
{
    private const string MirrorHosterClassName = "MirrorHoster";
    private const string TargetHosterClassName = "TargetHoster";

    private BearcatDbContext dbContext = null!;
    private string tempRootPath = null!;
    private string releaseFolderPath = null!;
    private string archiveFilesBasePath = null!;
    private string deletedArchiveFolderPath = null!;
    private RecordingDownloadHoster downloadHoster = null!;
    private Mock<IHosterFactory> hosterFactoryMock = null!;
    private Mock<IArchiver> archiverMock = null!;
    private Mock<IArchiverFactory> archiverFactoryMock = null!;
    private ArchiveRestoreService restoreService = null!;
    private ArchiveCreationService creationService = null!;

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
        deletedArchiveFolderPath = Path.Combine(tempRootPath, "gone");

        downloadHoster = new RecordingDownloadHoster();

        hosterFactoryMock = new Mock<IHosterFactory>(MockBehavior.Strict);
        hosterFactoryMock.Setup(f => f.GetByName(MirrorHosterClassName)).Returns(downloadHoster);
        hosterFactoryMock
            .Setup(f => f.GetByName(TargetHosterClassName))
            .Returns(Mock.Of<IHoster>());

        archiverMock = new Mock<IArchiver>(MockBehavior.Strict);
        archiverMock.SetupGet(a => a.Name).Returns("zip");
        archiverMock.SetupGet(a => a.FileExtension).Returns(".zip");
        archiverMock.SetupGet(a => a.CanChangeHashInPlace).Returns(true);

        archiverFactoryMock = new Mock<IArchiverFactory>(MockBehavior.Strict);

        var notificationService = new NotificationService(
            repository: new NotificationRepository(dbContext),
            timeProvider: CreateTimeProvider(),
            configurationProvider: CreateNotificationConfigurationProvider()
        );

        var downloadProgressTracker = new DownloadProgressTracker();

        restoreService = new ArchiveRestoreService(
            new ArchiveRestoreRepository(dbContext),
            hosterFactoryMock.Object,
            new FileSystemService(),
            NoOpSecretProtector.Instance,
            notificationService,
            new DefaultConfigurationProvider(),
            new DownloadCancellationRegistry(),
            new MirrorSourceResolver(hosterFactoryMock.Object),
            new MirrorDownloadCoordinator(
                downloadProgressTracker,
                new ArchiveFileDownloader(
                    downloadProgressTracker,
                    NullLogger<ArchiveFileDownloader>.Instance
                )
            ),
            NullLogger<ArchiveRestoreService>.Instance
        );

        creationService = new ArchiveCreationService(
            new ArchiveCreationRepository(dbContext),
            NullLogger<ArchiveCreationService>.Instance,
            archiverFactoryMock.Object,
            new FileSystemService(),
            CreateTimeProvider(),
            notificationService,
            new DefaultConfigurationProvider()
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
    public async Task ProcessAsync_MirrorIsAvailable_RestoresArchiveAndAssignsItWithoutRepacking()
    {
        // Arrange
        var scenario = await AddDeletedArchiveScenarioAsync(mirrorDownloadsEnabled: true);

        // Act
        await restoreService.ProcessAsync(CancellationToken.None);
        await creationService.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var archives = await dbContext
            .Archives.Include(a => a.ArchiveFiles)
            .OrderBy(a => a.Id)
            .ToListAsync();
        var waitingUpload = await dbContext.Uploads.SingleAsync(u =>
            u.Id == scenario.WaitingUploadId
        );

        archives.Count.ShouldBe(1);

        var restoredArchive = archives.Single();

        restoredArchive.Id.ShouldBe(scenario.ArchiveId);
        restoredArchive.ArchiveState.ShouldBe(ArchiveState.Created);
        restoredArchive.ArchiveFolderPath.ShouldStartWith(archiveFilesBasePath);
        restoredArchive
            .ArchiveFiles.Select(f => Path.GetFileName(f.FullFileName))
            .Order()
            .ShouldBe(["archive.part1.rar", "archive.part2.rar"]);
        restoredArchive.ArchiveFiles.ShouldAllBe(f => File.Exists(f.FullFileName));
        restoredArchive.ArchiveFiles.ShouldAllBe(f => f.Md5Hash != null);
        downloadHoster
            .DownloadedLinks.Order()
            .ShouldBe(["https://mirror.test/file-1", "https://mirror.test/file-2"]);
        waitingUpload.ArchiveId.ShouldBe(scenario.ArchiveId);
        waitingUpload.UploadState.ShouldBe(UploadState.Pending);
        archiverFactoryMock.Verify(f => f.GetByName(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ProcessAsync_NoMirrorIsAvailable_RepacksFromReleaseFolderAndAssignsNewArchive()
    {
        // Arrange
        var scenario = await AddDeletedArchiveScenarioAsync(mirrorDownloadsEnabled: false);
        archiverFactoryMock.Setup(f => f.GetByName("zip")).Returns(archiverMock.Object);
        archiverMock
            .Setup(a =>
                a.ArchiveAsync(
                    releaseFolderPath,
                    It.Is<string>(p => p.StartsWith(archiveFilesBasePath)),
                    "bearcat-release",
                    It.IsAny<int>(),
                    "secret",
                    It.IsAny<ArchiveOptions>(),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new ArchiveResult(true, ["repacked.part1.rar"], null));

        // Act
        await restoreService.ProcessAsync(CancellationToken.None);
        await creationService.ProcessAsync(CancellationToken.None);

        // Assert
        dbContext.ChangeTracker.Clear();
        var archives = await dbContext
            .Archives.Include(a => a.ArchiveFiles)
            .OrderBy(a => a.Id)
            .ToListAsync();
        var waitingUpload = await dbContext.Uploads.SingleAsync(u =>
            u.Id == scenario.WaitingUploadId
        );

        archives.Count.ShouldBe(2);

        var deletedArchive = archives.Single(a => a.Id == scenario.ArchiveId);
        var repackedArchive = archives.Single(a => a.Id != scenario.ArchiveId);

        deletedArchive.ArchiveState.ShouldBe(ArchiveState.Deleted);
        deletedArchive.ArchiveFolderPath.ShouldBe(deletedArchiveFolderPath);
        repackedArchive.ArchiveState.ShouldBe(ArchiveState.Created);
        repackedArchive.ArchiveFiles.Single().FullFileName.ShouldBe("repacked.part1.rar");
        waitingUpload.ArchiveId.ShouldBe(repackedArchive.Id);
        waitingUpload.UploadState.ShouldBe(UploadState.Pending);
        downloadHoster.DownloadedLinks.ShouldBeEmpty();
    }

    private async Task<DeletedArchiveScenario> AddDeletedArchiveScenarioAsync(
        bool mirrorDownloadsEnabled
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
        };
        var mirrorUploadConfig = new UploadConfig
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
            Name = "Mirror upload",
        };
        var targetUploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = new HosterRegistration
            {
                Name = "Target",
                SerializedConfig = "{}",
                HosterClassName = TargetHosterClassName,
                IsActive = true,
            },
            Name = "Target upload",
        };

        dbContext.UploadConfigs.AddRange(mirrorUploadConfig, targetUploadConfig);
        await dbContext.SaveChangesAsync();

        var firstArchiveFile = new ArchiveFile
        {
            FullFileName = Path.Combine(deletedArchiveFolderPath, "archive.part1.rar"),
        };
        var secondArchiveFile = new ArchiveFile
        {
            FullFileName = Path.Combine(deletedArchiveFolderPath, "archive.part2.rar"),
        };
        var archive = new Archive
        {
            ArchiveConfigId = archiveConfig.Id,
            ArchiveFolderPath = deletedArchiveFolderPath,
            ArchiveState = ArchiveState.Deleted,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            ArchiveFiles = [firstArchiveFile, secondArchiveFile],
            Uploads = [],
            ErrorMessages = [],
        };
        var mirrorUpload = new Upload
        {
            UploadConfigId = mirrorUploadConfig.Id,
            Archive = archive,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UploadedAt = DateTime.UtcNow.AddHours(-2),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            ErrorMessages = [],
            UploadedFiles =
            [
                new UploadedFile
                {
                    ArchiveFile = firstArchiveFile,
                    HosterFileLink = "https://mirror.test/file-1",
                    ExternalId = "external-1",
                    OnlineState = OnlineState.Online,
                    CreatedAt = DateTime.UtcNow.AddHours(-2),
                    CheckedAt = DateTime.UtcNow.AddHours(-2),
                },
                new UploadedFile
                {
                    ArchiveFile = secondArchiveFile,
                    HosterFileLink = "https://mirror.test/file-2",
                    ExternalId = "external-2",
                    OnlineState = OnlineState.Online,
                    CreatedAt = DateTime.UtcNow.AddHours(-2),
                    CheckedAt = DateTime.UtcNow.AddHours(-2),
                },
            ],
        };
        var waitingUpload = new Upload
        {
            UploadConfigId = targetUploadConfig.Id,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.WaitingForArchive,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
            UploadedFiles = [],
        };

        dbContext.Archives.Add(archive);
        dbContext.Uploads.AddRange(mirrorUpload, waitingUpload);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return new DeletedArchiveScenario(archive.Id, waitingUpload.Id);
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }

    private sealed record DeletedArchiveScenario(int ArchiveId, int WaitingUploadId);

    private sealed class DefaultConfigurationProvider : IApplicationConfigurationProvider
    {
        public TConfiguration GetConfiguration<TConfiguration>()
            where TConfiguration : IApplicationConfiguration, new() => new();

        public bool GetValue<TConfiguration>(Expression<Func<TConfiguration, bool>> selector)
            where TConfiguration : IApplicationConfiguration, new() =>
            selector.Compile()(new TConfiguration());

        public int GetValue<TConfiguration>(Expression<Func<TConfiguration, int>> selector)
            where TConfiguration : IApplicationConfiguration, new() =>
            selector.Compile()(new TConfiguration());

        public int? GetValue<TConfiguration>(Expression<Func<TConfiguration, int?>> selector)
            where TConfiguration : IApplicationConfiguration, new() =>
            selector.Compile()(new TConfiguration());

        public string? GetValue<TConfiguration>(Expression<Func<TConfiguration, string?>> selector)
            where TConfiguration : IApplicationConfiguration, new() =>
            selector.Compile()(new TConfiguration());

        public TValue GetValue<TConfiguration, TValue>(
            Expression<Func<TConfiguration, TValue>> selector
        )
            where TConfiguration : IApplicationConfiguration, new() =>
            selector.Compile()(new TConfiguration());
    }

    private sealed class RecordingDownloadHoster : IHosterWithDownload
    {
        public List<string> DownloadedLinks { get; } = [];

        public string Name => "Mirror";

        public bool SupportsPremiumOnlyDownloads => false;

        public bool DownloadRequiresPremium => false;

        public bool HasFixedParallelUploadLimit => false;

        public int? DefaultMaximumParallelUploads => 1;

        public IReadOnlyList<string> ConfigurationKeys => [];

        public Task<IReadOnlyDictionary<string, long>> GetFileSizesAsync(
            IReadOnlyList<string> fileUrls,
            IHosterConfig hosterConfig,
            CancellationToken cancellationToken
        )
        {
            IReadOnlyDictionary<string, long> sizes = fileUrls.ToDictionary(
                fileUrl => fileUrl,
                _ => (long)"mirror-payload".Length
            );

            return Task.FromResult(sizes);
        }

        public async Task<DownloadFileResult> DownloadFileAsync(
            DownloadFileDto file,
            string targetFilePath,
            IHosterConfig hosterConfig,
            IDownloadProgress progress,
            CancellationToken cancellationToken
        )
        {
            lock (DownloadedLinks)
            {
                DownloadedLinks.Add(file.HosterFileLink);
            }

            const string content = "mirror-payload";

            progress.BeginFile(content.Length);
            await File.WriteAllTextAsync(targetFilePath, content, cancellationToken);
            progress.ReportBytesTransferred(content.Length);

            return new DownloadFileResult(IsSuccess: true, ErrorMessages: []);
        }

        public IHosterConfig DeserializeHosterConfig(string serializedConfig) =>
            Mock.Of<IHosterConfig>();

        public string SerializeHosterConfig(Dictionary<string, string> hosterConfig) =>
            string.Empty;

        public Task<UploadFileResult> UploadFileAsync(
            FileDto fileDto,
            IHosterConfig hosterConfig,
            IUploadProgress progress,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public Task<FileExistResult> CheckFilesExistAsync(
            IHosterConfig hosterConfig,
            IReadOnlyList<FileUrlToCheckDto> files,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public Task<int?> GetMaximumParallelUploadsAsync(
            IHosterConfig hosterConfig,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public Task<TryLoginResult> TryLoginAsync(
            IHosterConfig hosterConfig,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();
    }
}
