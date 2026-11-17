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
            afterArchiveId: 0,
            maximumArchiveCount: 100,
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
            afterArchiveId: 0,
            maximumArchiveCount: 100,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [TestCase(-1, true)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    public async Task GetLocalArchivesPastRetentionAsync_LatestUploadRelativeToCutoff_ReturnsArchiveOnlyWhenUploadedAtOrBeforeCutoff(
        int uploadedMinutesAfterCutoff,
        bool expectedToBeReturned
    )
    {
        // Arrange
        await AddArchiveAsync("Archive", uploadedAt: Cutoff.AddMinutes(uploadedMinutesAfterCutoff));

        // Act
        var result = await repository.GetLocalArchivesPastRetentionAsync(
            Cutoff,
            afterArchiveId: 0,
            maximumArchiveCount: 100,
            CancellationToken.None
        );

        // Assert
        result.Count.ShouldBe(expectedToBeReturned ? 1 : 0);
    }

    [Test]
    public async Task GetLocalArchivesPastRetentionAsync_ArchiveWithFailedUploadWithoutUploadedAt_IsExcluded()
    {
        // Arrange
        await AddArchiveAsync(
            "PartlyUploaded",
            uploadedAt: Cutoff.AddDays(-1),
            failedUploadWithoutUploadedAt: FailedUploadWithoutUploadedAt.AfterCompletedUpload
        );

        // Act
        var result = await repository.GetLocalArchivesPastRetentionAsync(
            Cutoff,
            afterArchiveId: 0,
            maximumArchiveCount: 100,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetLocalArchivesPastRetentionAsync_FailedUploadReplacedByLaterCompletedUpload_ReturnsArchive()
    {
        // Arrange
        var archive = await AddArchiveAsync(
            "Reuploaded",
            uploadedAt: Cutoff.AddDays(-1),
            failedUploadWithoutUploadedAt: FailedUploadWithoutUploadedAt.BeforeCompletedUpload
        );

        // Act
        var result = await repository.GetLocalArchivesPastRetentionAsync(
            Cutoff,
            afterArchiveId: 0,
            maximumArchiveCount: 100,
            CancellationToken.None
        );

        // Assert
        result.ShouldHaveSingleItem().Id.ShouldBe(archive.Id);
    }

    [Test]
    public async Task GetLocalArchivesPastRetentionAsync_MoreArchivesThanMaximumCount_ReturnsArchivesWithLowestIdsAfterGivenArchiveId()
    {
        // Arrange
        var firstArchive = await AddArchiveAsync("First", uploadedAt: Cutoff.AddDays(-1));
        var secondArchive = await AddArchiveAsync("Second", uploadedAt: Cutoff.AddDays(-1));
        var thirdArchive = await AddArchiveAsync("Third", uploadedAt: Cutoff.AddDays(-1));
        await AddArchiveAsync("Fourth", uploadedAt: Cutoff.AddDays(-1));

        // Act
        var result = await repository.GetLocalArchivesPastRetentionAsync(
            Cutoff,
            afterArchiveId: firstArchive.Id,
            maximumArchiveCount: 2,
            CancellationToken.None
        );

        // Assert
        result.Select(archive => archive.Id).ShouldBe([secondArchive.Id, thirdArchive.Id]);
    }

    [Test]
    public async Task GetLocalArchivesPastRetentionAsync_MaximumCountReachedByCandidatesOnly_SkipsArchivesThatAreNoCandidates()
    {
        // Arrange
        var firstArchive = await AddArchiveAsync("First", uploadedAt: Cutoff.AddDays(-1));
        await AddArchiveAsync("Recent", uploadedAt: Cutoff.AddDays(1));
        var thirdArchive = await AddArchiveAsync("Third", uploadedAt: Cutoff.AddDays(-1));

        // Act
        var result = await repository.GetLocalArchivesPastRetentionAsync(
            Cutoff,
            afterArchiveId: 0,
            maximumArchiveCount: 2,
            CancellationToken.None
        );

        // Assert
        result.Select(archive => archive.Id).ShouldBe([firstArchive.Id, thirdArchive.Id]);
    }

    [Test]
    public async Task GetLocalArchivesPastRetentionAsync_OverdueArchiveWithMirrorRegistration_LoadsUploadsWithUploadedFilesOfArchiveConfig()
    {
        // Arrange
        var archive = await AddArchiveAsync(
            "Overdue",
            uploadedAt: Cutoff.AddDays(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"],
            hosterRegistrationIsActive: true,
            hosterRegistrationUseForMirrorDownloads: true
        );

        // Act
        var result = await repository.GetLocalArchivesPastRetentionAsync(
            Cutoff,
            afterArchiveId: 0,
            maximumArchiveCount: 100,
            CancellationToken.None
        );

        // Assert
        var candidate = result.ShouldHaveSingleItem();
        candidate.Id.ShouldBe(archive.Id);
        var uploadConfig = candidate.ArchiveConfig.UploadConfigs.ShouldHaveSingleItem();
        uploadConfig.HosterRegistration!.Name.ShouldBe("Overdue hoster");
        uploadConfig
            .Uploads.ShouldHaveSingleItem()
            .UploadedFiles.Select(file => file.HosterFileLink)
            .ShouldBe(["https://hoster.test/1", "https://hoster.test/2"], ignoreOrder: true);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public async Task GetLocalArchivesPastRetentionAsync_HosterRegistrationNotUsableForMirrorDownloads_LoadsUploadsWithoutUploadedFiles(
        bool hosterRegistrationIsActive,
        bool hosterRegistrationUseForMirrorDownloads
    )
    {
        // Arrange
        await AddArchiveAsync(
            "Overdue",
            uploadedAt: Cutoff.AddDays(-1),
            uploadedFileLinks: ["https://hoster.test/1", "https://hoster.test/2"],
            hosterRegistrationIsActive: hosterRegistrationIsActive,
            hosterRegistrationUseForMirrorDownloads: hosterRegistrationUseForMirrorDownloads
        );

        // Act
        var result = await repository.GetLocalArchivesPastRetentionAsync(
            Cutoff,
            afterArchiveId: 0,
            maximumArchiveCount: 100,
            CancellationToken.None
        );

        // Assert
        result
            .ShouldHaveSingleItem()
            .ArchiveConfig.UploadConfigs.ShouldHaveSingleItem()
            .Uploads.ShouldHaveSingleItem()
            .UploadedFiles.ShouldBeEmpty();
    }

    [Test]
    public async Task GetDeletableLocalArchivesOrderedByLastUploadOfArchiveConfigAsync_SeveralArchives_ReturnsArchivesOrderedByLastUploadThenId()
    {
        // Arrange
        var newestArchive = await AddArchiveAsync("Newest", uploadedAt: Cutoff.AddDays(2));
        var oldestArchive = await AddArchiveAsync("Oldest", uploadedAt: Cutoff.AddDays(-5));
        var firstMiddleArchive = await AddArchiveAsync("FirstMiddle", uploadedAt: Cutoff);
        var secondMiddleArchive = await AddArchiveAsync("SecondMiddle", uploadedAt: Cutoff);

        // Act
        var result =
            await repository.GetDeletableLocalArchivesOrderedByLastUploadOfArchiveConfigAsync(
                CancellationToken.None
            );

        // Assert
        result
            .Select(archive => archive.Id)
            .ShouldBe([
                oldestArchive.Id,
                firstMiddleArchive.Id,
                secondMiddleArchive.Id,
                newestArchive.Id,
            ]);
        result.ShouldAllBe(archive => archive.ArchiveFiles.Count == 2);
        result.ShouldAllBe(archive => archive.ArchiveConfig.Release != null);
    }

    [Test]
    public async Task GetDeletableLocalArchivesOrderedByLastUploadOfArchiveConfigAsync_ArchivesThatMustStay_AreExcluded()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync("NAS", isActive: true);
        await AddArchiveAsync(
            "Stored",
            uploadedAt: Cutoff,
            archiveStorageFolderId: storageFolder.Id
        );
        await AddArchiveAsync(
            "WaitingForArchive",
            uploadedAt: Cutoff,
            uploadState: UploadState.WaitingForArchive
        );
        await AddArchiveAsync("Deleted", uploadedAt: Cutoff, archiveState: ArchiveState.Deleted);
        await AddArchiveAsync(
            "PartlyUploaded",
            uploadedAt: Cutoff,
            failedUploadWithoutUploadedAt: FailedUploadWithoutUploadedAt.AfterCompletedUpload
        );
        var deletableArchive = await AddArchiveAsync("Deletable", uploadedAt: Cutoff);

        // Act
        var result =
            await repository.GetDeletableLocalArchivesOrderedByLastUploadOfArchiveConfigAsync(
                CancellationToken.None
            );

        // Assert
        result.ShouldHaveSingleItem().Id.ShouldBe(deletableArchive.Id);
    }

    [Test]
    public async Task GetLocalCreatedArchiveFilePathsAsync_MixedArchives_ReturnsFilesOfCreatedLocalArchivesOnly()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync("NAS", isActive: true);
        await AddArchiveAsync("Local", uploadedAt: Cutoff, uploadState: UploadState.Uploading);
        await AddArchiveAsync(
            "Stored",
            uploadedAt: Cutoff,
            archiveStorageFolderId: storageFolder.Id
        );
        await AddArchiveAsync("Deleted", uploadedAt: Cutoff, archiveState: ArchiveState.Deleted);

        // Act
        var result = await repository.GetLocalCreatedArchiveFilePathsAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(
            ["/archives/Local/Local.part1.rar", "/archives/Local/Local.part2.rar"],
            ignoreOrder: true
        );
    }

    [Test]
    public async Task HasUnresolvedNotificationAsync_ResolvedNotificationAndOtherKind_ReturnsTrueOnlyForUnresolvedKind()
    {
        // Arrange
        await AddNotificationAsync(
            NotificationKind.LocalArchiveSizeLimitExceeded,
            archiveId: null,
            resolvedAt: Cutoff
        );
        await AddNotificationAsync(
            NotificationKind.NoArchiveStorageFolderAvailable,
            archiveId: null,
            resolvedAt: null
        );

        // Act
        var hasUnresolvedSizeLimitNotification = await repository.HasUnresolvedNotificationAsync(
            NotificationKind.LocalArchiveSizeLimitExceeded,
            CancellationToken.None
        );
        var hasUnresolvedStorageFolderNotification =
            await repository.HasUnresolvedNotificationAsync(
                NotificationKind.NoArchiveStorageFolderAvailable,
                CancellationToken.None
            );

        // Assert
        hasUnresolvedSizeLimitNotification.ShouldBeFalse();
        hasUnresolvedStorageFolderNotification.ShouldBeTrue();
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

    [Test]
    public async Task GetArchivesWithLocalWorkingCopyFilesWithoutWaitingPendingOrUploadingUploadAsync_MixedArchives_ReturnsCreatedArchivesInStorageFolderWithLocalWorkingCopyFilesOrderedById()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync("NAS", isActive: true);
        var firstArchive = await AddArchiveAsync(
            "First",
            Cutoff,
            archiveStorageFolderId: storageFolder.Id,
            archiveFileFolderPath: "/working-copies/First"
        );
        var secondArchive = await AddArchiveAsync(
            "Second",
            Cutoff,
            archiveStorageFolderId: storageFolder.Id,
            archiveFileFolderPath: "/working-copies/Second"
        );
        await AddArchiveAsync(
            "StoredWithoutWorkingCopy",
            Cutoff,
            archiveStorageFolderId: storageFolder.Id
        );
        await AddArchiveAsync("Local", Cutoff, archiveFileFolderPath: "/working-copies/Local");
        await AddArchiveAsync(
            "Deleted",
            Cutoff,
            archiveStorageFolderId: storageFolder.Id,
            archiveState: ArchiveState.Deleted,
            archiveFileFolderPath: "/working-copies/Deleted"
        );

        // Act
        var result =
            await repository.GetArchivesWithLocalWorkingCopyFilesWithoutWaitingPendingOrUploadingUploadAsync(
                CancellationToken.None
            );

        // Assert
        result.Select(archive => archive.Id).ShouldBe([firstArchive.Id, secondArchive.Id]);
        result.ShouldAllBe(archive => archive.ArchiveFiles.Count == 2);
    }

    [Test]
    public async Task GetArchivesWithLocalWorkingCopyFilesWithoutWaitingPendingOrUploadingUploadAsync_ArchiveWithLocalWorkingCopyFile_ReturnsArchive()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync("NAS", isActive: true);
        var archive = await AddArchiveAsync(
            "Stored",
            Cutoff,
            archiveStorageFolderId: storageFolder.Id,
            archiveFileFolderPath: "/working-copies/Stored"
        );

        // Act
        var result =
            await repository.GetArchivesWithLocalWorkingCopyFilesWithoutWaitingPendingOrUploadingUploadAsync(
                CancellationToken.None
            );

        // Assert
        result.ShouldHaveSingleItem().Id.ShouldBe(archive.Id);
    }

    [Test]
    public async Task GetArchivesWithLocalWorkingCopyFilesWithoutWaitingPendingOrUploadingUploadAsync_ArchiveWithoutLocalWorkingCopyFiles_ReturnsNothing()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync("NAS", isActive: true);
        await AddArchiveAsync("Stored", Cutoff, archiveStorageFolderId: storageFolder.Id);

        // Act
        var result =
            await repository.GetArchivesWithLocalWorkingCopyFilesWithoutWaitingPendingOrUploadingUploadAsync(
                CancellationToken.None
            );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetArchivesWithLocalWorkingCopyFilesWithoutWaitingPendingOrUploadingUploadAsync_FileInSiblingFolderWithSamePrefix_ReturnsArchive()
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync("NAS", isActive: true);
        var archive = await AddArchiveAsync(
            "Rel",
            Cutoff,
            archiveStorageFolderId: storageFolder.Id,
            archiveFileFolderPath: "/archives/Rel.5"
        );

        // Act
        var result =
            await repository.GetArchivesWithLocalWorkingCopyFilesWithoutWaitingPendingOrUploadingUploadAsync(
                CancellationToken.None
            );

        // Assert
        result.ShouldHaveSingleItem().Id.ShouldBe(archive.Id);
    }

    [TestCase(UploadState.WaitingForArchive, false)]
    [TestCase(UploadState.Pending, false)]
    [TestCase(UploadState.Uploading, false)]
    [TestCase(UploadState.Completed, true)]
    [TestCase(UploadState.Failed, true)]
    public async Task GetArchivesWithLocalWorkingCopyFilesWithoutWaitingPendingOrUploadingUploadAsync_UploadInState_ReturnsArchiveOnlyWithoutActiveUpload(
        UploadState uploadState,
        bool expectedToBeReturned
    )
    {
        // Arrange
        var storageFolder = await AddStorageFolderAsync("NAS", isActive: true);
        var archive = await AddArchiveAsync(
            "Stored",
            Cutoff,
            archiveStorageFolderId: storageFolder.Id,
            uploadState: uploadState,
            archiveFileFolderPath: "/working-copies/Stored"
        );

        // Act
        var result =
            await repository.GetArchivesWithLocalWorkingCopyFilesWithoutWaitingPendingOrUploadingUploadAsync(
                CancellationToken.None
            );

        // Assert
        result.Any(candidate => candidate.Id == archive.Id).ShouldBe(expectedToBeReturned);
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
        UploadState uploadState = UploadState.Completed,
        ArchiveState archiveState = ArchiveState.Created,
        string? archiveFileFolderPath = null,
        IReadOnlyList<string>? uploadedFileLinks = null,
        FailedUploadWithoutUploadedAt failedUploadWithoutUploadedAt =
            FailedUploadWithoutUploadedAt.None,
        bool hosterRegistrationIsActive = true,
        bool hosterRegistrationUseForMirrorDownloads = false
    )
    {
        var archiveFolderPath = $"/archives/{name}";
        var archiveFileFolder = archiveFileFolderPath ?? archiveFolderPath;
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
            UploadSource = UploadSource.ArchiveFiles,
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = new HosterRegistration
            {
                Name = $"{name} hoster",
                SerializedConfig = "{}",
                HosterClassName = "Hoster",
                IsActive = hosterRegistrationIsActive,
                UseForMirrorDownloads = hosterRegistrationUseForMirrorDownloads,
            },
            Name = "Default upload",
        };
        var archiveFiles = new List<ArchiveFile>
        {
            new() { FullFileName = $"{archiveFileFolder}/{name}.part1.rar" },
            new() { FullFileName = $"{archiveFileFolder}/{name}.part2.rar" },
        };
        var archive = new Archive
        {
            ArchiveConfig = archiveConfig,
            ArchiveFolderPath = archiveFolderPath,
            ArchiveStorageFolderId = archiveStorageFolderId,
            ArchiveState = archiveState,
            ArchiveFileSizeMb = 512,
            CreatedAt = Cutoff.AddDays(-30),
            ArchiveFiles = archiveFiles,
            Uploads = [],
            ErrorMessages = [],
        };

        var completedUpload = new Upload
        {
            UploadConfig = uploadConfig,
            HosterRegistration = uploadConfig.HosterRegistration,
            CreatedAt = uploadedAt.AddHours(-1),
            UploadedAt = uploadedAt,
            UploadState = uploadState,
            OnlineState = OnlineState.Online,
            ErrorMessages = [],
            UploadedFiles = (uploadedFileLinks ?? [])
                .Select(
                    (link, index) =>
                        new UploadedFile
                        {
                            ArchiveFile = archiveFiles[index],
                            HosterFileLink = link,
                            OnlineState = OnlineState.Online,
                            CreatedAt = uploadedAt,
                            ErrorMessages = [],
                        }
                )
                .ToList(),
        };

        var failedUpload = new Upload
        {
            UploadConfig = uploadConfig,
            HosterRegistration = uploadConfig.HosterRegistration,
            CreatedAt = uploadedAt.AddHours(-1),
            UploadState = UploadState.Failed,
            OnlineState = OnlineState.Unknown,
            ErrorMessages = [],
            UploadedFiles = [],
        };

        DbContext.Archives.Add(archive);

        if (failedUploadWithoutUploadedAt is FailedUploadWithoutUploadedAt.BeforeCompletedUpload)
        {
            archive.Uploads.Add(failedUpload);
            await DbContext.SaveChangesAsync();
        }

        archive.Uploads.Add(completedUpload);
        await DbContext.SaveChangesAsync();

        if (failedUploadWithoutUploadedAt is FailedUploadWithoutUploadedAt.AfterCompletedUpload)
        {
            archive.Uploads.Add(failedUpload);
            await DbContext.SaveChangesAsync();
        }

        DbContext.ChangeTracker.Clear();

        return archive;
    }

    private enum FailedUploadWithoutUploadedAt
    {
        None,
        BeforeCompletedUpload,
        AfterCompletedUpload,
    }
}
