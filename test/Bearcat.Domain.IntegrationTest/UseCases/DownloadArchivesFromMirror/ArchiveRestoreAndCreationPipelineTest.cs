using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.Hoster.Dto;
using Bearcat.Abstractions.Hoster.Results;
using Bearcat.Abstractions.Proxies;
using Bearcat.Abstractions.Transfers;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;
using Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Sources;
using Bearcat.Domain.UseCases.ManageArchives;
using Bearcat.Domain.UseCases.ManageArchives.ReleaseFolderEntriesForPacking;
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

public class ArchiveRestoreAndCreationPipelineTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string MirrorHosterClassName = "MirrorHoster";
    private const string TargetHosterClassName = "TargetHoster";

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
            repository: new NotificationRepository(DbContext),
            timeProvider: CreateTimeProvider(),
            configurationProvider: CreateNotificationConfigurationProvider()
        );

        var transferProgressTracker = new TransferProgressTracker(Mock.Of<IProxyRoutingCache>());

        restoreService = new ArchiveRestoreService(
            new ArchiveRestoreRepository(DbContext),
            hosterFactoryMock.Object,
            new FileSystemService(),
            NoOpSecretProtector.Instance,
            notificationService,
            new DefaultConfigurationProvider(),
            new TransferCancellationRegistry(),
            new MirrorSourceResolver(hosterFactoryMock.Object),
            new MirrorDownloadCoordinator(
                transferProgressTracker,
                new ArchiveFileDownloader(
                    transferProgressTracker,
                    new TransferSpeedLimitService(new DefaultConfigurationProvider()),
                    NullLogger<ArchiveFileDownloader>.Instance
                )
            ),
            NullLogger<ArchiveRestoreService>.Instance
        );

        creationService = new ArchiveCreationService(
            new ArchiveCreationRepository(DbContext),
            NullLogger<ArchiveCreationService>.Instance,
            archiverFactoryMock.Object,
            new FileSystemService(),
            CreateTimeProvider(),
            notificationService,
            new DefaultConfigurationProvider(),
            new ReleaseFolderEntriesForPackingService(new FileSystemService()),
            transferProgressTracker,
            new TransferCancellationRegistry(),
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
    public async Task ProcessAsync_MirrorIsAvailable_RestoresArchiveAndAssignsItWithoutRepacking()
    {
        // Arrange
        var scenario = await AddDeletedArchiveScenarioAsync(mirrorDownloadsEnabled: true);
        archiverFactoryMock.Setup(f => f.GetByName("zip")).Returns(archiverMock.Object);

        // Act
        await restoreService.ProcessAsync(CancellationToken.None);
        await creationService.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var archives = await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .OrderBy(a => a.Id)
            .ToListAsync();
        var waitingUpload = await DbContext.Uploads.SingleAsync(u =>
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
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ArchiveResult(true, ["repacked.part1.rar"], null));

        // Act
        await restoreService.ProcessAsync(CancellationToken.None);
        await creationService.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var archives = await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .OrderBy(a => a.Id)
            .ToListAsync();
        var waitingUpload = await DbContext.Uploads.SingleAsync(u =>
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

    [Test]
    public async Task ProcessAsync_FileToUploadIsMissingLocally_RestoresArchiveBeforeChangingEachHashOnce()
    {
        // Arrange
        var scenario = await AddCreatedArchiveWithMissingFileScenarioAsync();
        archiverFactoryMock.Setup(f => f.GetByName("zip")).Returns(archiverMock.Object);

        // Act
        await restoreService.ProcessAsync(CancellationToken.None);
        await creationService.ProcessAsync(CancellationToken.None);
        DbContext.ChangeTracker.Clear();
        var archiveStateAfterFirstRun = (
            await DbContext.Archives.SingleAsync(a => a.Id == scenario.ArchiveId)
        ).ArchiveState;
        DbContext.ChangeTracker.Clear();
        await restoreService.ProcessAsync(CancellationToken.None);
        await creationService.ProcessAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var archive = await DbContext
            .Archives.Include(a => a.ArchiveFiles)
            .SingleAsync(a => a.Id == scenario.ArchiveId);
        var waitingUpload = await DbContext
            .Uploads.Include(u => u.UploadedFiles)
            .SingleAsync(u => u.Id == scenario.WaitingUploadId);
        var archiveFilesByName = archive.ArchiveFiles.ToDictionary(f =>
            Path.GetFileName(f.FullFileName)
        );

        archiveStateAfterFirstRun.ShouldBe(ArchiveState.MissingFiles);
        archive.ArchiveState.ShouldBe(ArchiveState.Created);
        (await DbContext.Archives.CountAsync()).ShouldBe(1);
        waitingUpload.ArchiveId.ShouldBe(scenario.ArchiveId);
        waitingUpload.UploadState.ShouldBe(UploadState.Pending);
        waitingUpload
            .UploadedFiles.ShouldHaveSingleItem()
            .ArchiveFileId.ShouldBe(archiveFilesByName["archive.part1.rar"].Id);
        downloadHoster
            .DownloadedLinks.Order()
            .ShouldBe(["https://mirror.test/file-2", "https://mirror.test/file-3"]);
        archiveFilesByName["archive.part1.rar"].FullFileName.ShouldBe(scenario.CarriedOverFilePath);
        (await File.ReadAllTextAsync(scenario.CarriedOverFilePath)).ShouldBe("carried-over");
        (await File.ReadAllTextAsync(scenario.LocallyPresentFilePath)).ShouldBe("locally-present");

        foreach (
            var (fileName, link) in new[]
            {
                ("archive.part2.rar", "https://mirror.test/file-2"),
                ("archive.part3.rar", "https://mirror.test/file-3"),
            }
        )
        {
            var archiveFile = archiveFilesByName[fileName];
            var fileBytes = await File.ReadAllBytesAsync(archiveFile.FullFileName);

            archiveFile.FullFileName.ShouldStartWith(archive.ArchiveFolderPath);
            fileBytes.ShouldBe([.. Encoding.UTF8.GetBytes(GetMirrorFileContent(link)), (byte)0]);
            archiveFile.Md5Hash.ShouldBe(Convert.ToHexString(MD5.HashData(fileBytes)));
        }
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

        DbContext.UploadConfigs.AddRange(mirrorUploadConfig, targetUploadConfig);
        await DbContext.SaveChangesAsync();

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

        DbContext.Archives.Add(archive);
        DbContext.Uploads.AddRange(mirrorUpload, waitingUpload);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return new DeletedArchiveScenario(archive.Id, waitingUpload.Id);
    }

    private async Task<CreatedArchiveWithMissingFileScenario> AddCreatedArchiveWithMissingFileScenarioAsync()
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
                UseForMirrorDownloads = true,
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

        DbContext.UploadConfigs.AddRange(mirrorUploadConfig, targetUploadConfig);
        await DbContext.SaveChangesAsync();

        var localArchiveFolderPath = Directory
            .CreateDirectory(Path.Combine(archiveFilesBasePath, "local"))
            .FullName;
        var carriedOverFilePath = Path.Combine(localArchiveFolderPath, "archive.part1.rar");
        var locallyPresentFilePath = Path.Combine(localArchiveFolderPath, "archive.part2.rar");
        await File.WriteAllTextAsync(carriedOverFilePath, "carried-over");
        await File.WriteAllTextAsync(locallyPresentFilePath, "locally-present");

        var carriedOverArchiveFile = new ArchiveFile
        {
            FullFileName = carriedOverFilePath,
            Md5Hash = Convert.ToHexString(
                MD5.HashData(await File.ReadAllBytesAsync(carriedOverFilePath))
            ),
        };
        var locallyPresentArchiveFile = new ArchiveFile
        {
            FullFileName = locallyPresentFilePath,
            Md5Hash = Convert.ToHexString(
                MD5.HashData(await File.ReadAllBytesAsync(locallyPresentFilePath))
            ),
        };
        var missingArchiveFile = new ArchiveFile
        {
            FullFileName = Path.Combine(localArchiveFolderPath, "archive.part3.rar"),
            Md5Hash = "0123456789ABCDEF0123456789ABCDEF",
        };
        var archive = new Archive
        {
            ArchiveConfigId = archiveConfig.Id,
            ArchiveFolderPath = localArchiveFolderPath,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            ArchiveFiles = [carriedOverArchiveFile, locallyPresentArchiveFile, missingArchiveFile],
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
                CreateUploadedFile(carriedOverArchiveFile, "https://mirror.test/file-1"),
                CreateUploadedFile(locallyPresentArchiveFile, "https://mirror.test/file-2"),
                CreateUploadedFile(missingArchiveFile, "https://mirror.test/file-3"),
            ],
        };
        var previousTargetUpload = new Upload
        {
            UploadConfigId = targetUploadConfig.Id,
            Archive = archive,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UploadedAt = DateTime.UtcNow.AddHours(-2),
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.PartiallyOnline,
            ErrorMessages = [],
            UploadedFiles =
            [
                CreateUploadedFile(carriedOverArchiveFile, "https://target.test/file-1"),
                CreateUploadedFile(
                    locallyPresentArchiveFile,
                    "https://target.test/file-2",
                    OnlineState.Offline
                ),
                CreateUploadedFile(
                    missingArchiveFile,
                    "https://target.test/file-3",
                    OnlineState.Offline
                ),
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

        DbContext.Archives.Add(archive);
        DbContext.Uploads.AddRange(mirrorUpload, previousTargetUpload, waitingUpload);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return new CreatedArchiveWithMissingFileScenario(
            ArchiveId: archive.Id,
            WaitingUploadId: waitingUpload.Id,
            CarriedOverFilePath: carriedOverFilePath,
            LocallyPresentFilePath: locallyPresentFilePath
        );
    }

    private static UploadedFile CreateUploadedFile(
        ArchiveFile archiveFile,
        string hosterFileLink,
        OnlineState onlineState = OnlineState.Online
    )
    {
        return new UploadedFile
        {
            ArchiveFile = archiveFile,
            HosterFileLink = hosterFileLink,
            OnlineState = onlineState,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            CheckedAt = DateTime.UtcNow.AddHours(-2),
        };
    }

    private static string GetMirrorFileContent(string hosterFileLink)
    {
        return $"mirror-payload {hosterFileLink}";
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }

    private sealed record DeletedArchiveScenario(int ArchiveId, int WaitingUploadId);

    private sealed record CreatedArchiveWithMissingFileScenario(
        int ArchiveId,
        int WaitingUploadId,
        string CarriedOverFilePath,
        string LocallyPresentFilePath
    );

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

        public decimal? GetValue<TConfiguration>(
            Expression<Func<TConfiguration, decimal?>> selector
        )
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
                fileUrl => (long)Encoding.UTF8.GetByteCount(GetMirrorFileContent(fileUrl))
            );

            return Task.FromResult(sizes);
        }

        public async Task<DownloadFileResult> DownloadFileAsync(
            DownloadFileDto file,
            string targetFilePath,
            IHosterConfig hosterConfig,
            ITransferProgress progress,
            CancellationToken cancellationToken
        )
        {
            lock (DownloadedLinks)
            {
                DownloadedLinks.Add(file.HosterFileLink);
            }

            var content = GetMirrorFileContent(file.HosterFileLink);

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
            ITransferProgress progress,
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
