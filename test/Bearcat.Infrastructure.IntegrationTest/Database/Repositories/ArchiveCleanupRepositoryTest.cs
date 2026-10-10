using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class ArchiveCleanupRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime Cutoff = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private ArchiveCleanupRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new ArchiveCleanupRepository(DbContext);
    }

    [Test]
    public async Task GetLocalArchivesPastRetentionAsync_OverdueLocalArchive_ReturnsArchiveWithReleaseAndFiles()
    {
        // Arrange
        var archive = await AddArchiveAsync("Overdue", uploadedAt: Cutoff.AddDays(-1));

        // Act
        var result = await repository.GetLocalArchivesPastRetentionAsync(
            Cutoff,
            CancellationToken.None
        );

        // Assert
        var candidate = result.ShouldHaveSingleItem();
        candidate.Id.ShouldBe(archive.Id);
        candidate.ArchiveConfig.Release.Name.ShouldBe("Overdue");
        candidate.ArchiveFiles.Count.ShouldBe(2);
    }

    [Test]
    public async Task GetLocalArchivesPastRetentionAsync_ArchiveInStorageFolderOrNotOverdue_IsExcluded()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync("NAS", isActive: true);
        await AddArchiveAsync(
            "Stored",
            uploadedAt: Cutoff.AddDays(-1),
            archiveStorageFolderId: storageFolder.Id
        );
        await AddArchiveAsync("Recent", uploadedAt: Cutoff.AddDays(1));

        // Act
        var result = await repository.GetLocalArchivesPastRetentionAsync(
            Cutoff,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [TestCase(UploadState.WaitingForArchive, true)]
    [TestCase(UploadState.Pending, true)]
    [TestCase(UploadState.Uploading, true)]
    [TestCase(UploadState.Completed, false)]
    public async Task HasWaitingPendingOrUploadingUploadAsync_UploadInState_ReturnsExpectedResult(
        UploadState uploadState,
        bool expectedResult
    )
    {
        // Arrange
        var archive = await AddArchiveAsync("Archive", Cutoff, uploadState: uploadState);
        await AddArchiveAsync("Other", Cutoff, uploadState: UploadState.Uploading);

        // Act
        var result = await repository.HasWaitingPendingOrUploadingUploadAsync(
            archive.ArchiveConfigId,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Test]
    public async Task GetActiveArchiveStorageFoldersAsync_ActiveAndInactiveFolders_ReturnsActiveFolders()
    {
        // Arrange
        var activeFolder = await AddStorageFolderAsync("NAS", isActive: true);
        await AddStorageFolderAsync("Disk", isActive: false);

        // Act
        var result = await repository.GetActiveArchiveStorageFoldersAsync(CancellationToken.None);

        // Assert
        result.ShouldHaveSingleItem().Id.ShouldBe(activeFolder.Id);
    }

    [Test]
    public async Task GetArchiveIdsWithUnresolvedNotificationAsync_MixedNotifications_ReturnsArchiveIdsOfUnresolvedNotificationsOfKind()
    {
        // Arrange
        var unresolvedArchive = await AddArchiveAsync("Unresolved", Cutoff);
        var resolvedArchive = await AddArchiveAsync("Resolved", Cutoff);
        var otherKindArchive = await AddArchiveAsync("OtherKind", Cutoff);
        await AddNotificationAsync(
            NotificationKind.NoArchiveStorageFolderAvailable,
            unresolvedArchive.Id,
            resolvedAt: null
        );
        await AddNotificationAsync(
            NotificationKind.NoArchiveStorageFolderAvailable,
            unresolvedArchive.Id,
            resolvedAt: null
        );
        await AddNotificationAsync(
            NotificationKind.NoArchiveStorageFolderAvailable,
            resolvedArchive.Id,
            resolvedAt: Cutoff
        );
        await AddNotificationAsync(
            NotificationKind.ArchiveMoveToStorageFolderFailed,
            otherKindArchive.Id,
            resolvedAt: null
        );
        await AddNotificationAsync(
            NotificationKind.NoArchiveStorageFolderAvailable,
            archiveId: null,
            resolvedAt: null
        );

        // Act
        var result = await repository.GetArchiveIdsWithUnresolvedNotificationAsync(
            NotificationKind.NoArchiveStorageFolderAvailable,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([unresolvedArchive.Id]);
    }

    private async Task<ArchiveStorageFolder> AddStorageFolderAsync(string name, bool isActive)
    {
        var storageFolder = new ArchiveStorageFolder
        {
            Name = name,
            Path = $"/mnt/{name.ToLowerInvariant()}",
            IsActive = isActive,
            MinimumFreeSpaceGb = 0,
            Priority = 1,
        };

        DbContext.ArchiveStorageFolders.Add(storageFolder);
        await DbContext.SaveChangesAsync();

        return storageFolder;
    }

    private async Task AddNotificationAsync(
        NotificationKind kind,
        int? archiveId,
        DateTime? resolvedAt
    )
    {
        DbContext.Notifications.Add(
            new Notification
            {
                CreatedAt = Cutoff,
                ResolvedAt = resolvedAt,
                NotificationKind = kind,
                NotificationSeverity = NotificationSeverity.Warning,
                Message = "Message",
                ArchiveId = archiveId,
            }
        );
        await DbContext.SaveChangesAsync();
    }

    private async Task<Archive> AddArchiveAsync(
        string name,
        DateTime uploadedAt,
        int? archiveStorageFolderId = null,
        UploadState uploadState = UploadState.Completed
    )
    {
        var release = new Release
        {
            Name = name,
            ReleaseType = ReleaseType.Unmanaged,
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = "Main archive",
            ArchiveFilesBasePath = "/archives",
            ArchiverName = "zip",
            ArchiveNamePrefix = name,
            ArchivePassword = "secret",
            ArchiveFileSizeMb = 512,
        };
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = new HosterRegistration
            {
                Name = $"{name} hoster",
                SerializedConfig = "{}",
                HosterClassName = "Hoster",
                IsActive = true,
            },
            Name = "Default upload",
        };
        var archive = new Archive
        {
            ArchiveConfig = archiveConfig,
            ArchiveFolderPath = $"/archives/{name}",
            ArchiveStorageFolderId = archiveStorageFolderId,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 512,
            CreatedAt = Cutoff.AddDays(-30),
            ArchiveFiles =
            [
                new ArchiveFile { FullFileName = $"/archives/{name}/{name}.part1.rar" },
                new ArchiveFile { FullFileName = $"/archives/{name}/{name}.part2.rar" },
            ],
            Uploads =
            [
                new Upload
                {
                    UploadConfig = uploadConfig,
                    CreatedAt = uploadedAt.AddHours(-1),
                    UploadedAt = uploadedAt,
                    UploadState = uploadState,
                    OnlineState = OnlineState.Online,
                    ErrorMessages = [],
                    UploadedFiles = [],
                },
            ],
            ErrorMessages = [],
        };

        DbContext.Archives.Add(archive);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return archive;
    }
}
