using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories;

public class UploadReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime Midnight = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    private UploadReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        repository = new UploadReadRepository(DbContext);
    }

    [Test]
    public async Task SearchUploadsAsync_NoFilter_ReturnsAllUploadsOrderedByCreatedAtDescending()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(6);
        result
            .Items.Select(item => item.UploadId)
            .ShouldBe([
                uploads.LatestUploadOfSecondHoster.Id,
                uploads.PendingUpload.Id,
                uploads.UploadedJustAfterMidnight.Id,
                uploads.OfflineUpload.Id,
                uploads.UploadedAtMidnight.Id,
                uploads.UploadedBeforeMidnight.Id,
            ]);
    }

    [Test]
    public async Task SearchUploadsAsync_UploadedAfterMidnight_ReturnsLaterUploadsOrderedByUploadedAt()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(UploadedAfter: Midnight),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(3);
        result
            .Items.Select(item => item.UploadId)
            .ShouldBe([
                uploads.UploadedJustAfterMidnight.Id,
                uploads.OfflineUpload.Id,
                uploads.LatestUploadOfSecondHoster.Id,
            ]);
    }

    [Test]
    public async Task SearchUploadsAsync_UploadStateFilter_ReturnsMatchingUploads()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(UploadState: UploadState.Pending),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Select(item => item.UploadId).ShouldBe([uploads.PendingUpload.Id]);
    }

    [Test]
    public async Task SearchUploadsAsync_OnlineStateFilter_ReturnsMatchingUploads()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(OnlineState: OnlineState.Offline),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Select(item => item.UploadId).ShouldBe([uploads.OfflineUpload.Id]);
    }

    [Test]
    public async Task SearchUploadsAsync_HosterRegistrationFilter_ReturnsUploadsOfHoster()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(HosterRegistrationId: uploads.FirstHosterRegistrationId),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(3);
        result
            .Items.Select(item => item.UploadId)
            .ShouldBe([
                uploads.UploadedJustAfterMidnight.Id,
                uploads.UploadedAtMidnight.Id,
                uploads.UploadedBeforeMidnight.Id,
            ]);
    }

    [Test]
    public async Task SearchUploadsAsync_ReleaseFilter_ReturnsUploadsOfRelease()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(ReleaseId: uploads.FirstReleaseId),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(4);
        result
            .Items.Select(item => item.UploadId)
            .ShouldBe([
                uploads.LatestUploadOfSecondHoster.Id,
                uploads.UploadedJustAfterMidnight.Id,
                uploads.UploadedAtMidnight.Id,
                uploads.UploadedBeforeMidnight.Id,
            ]);
    }

    [Test]
    public async Task SearchUploadsAsync_SecondPage_ReturnsRemainingUploadAndTotalCount()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(PageIndex: 1, PageSize: 5),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(6);
        result.PageIndex.ShouldBe(1);
        result.PageSize.ShouldBe(5);
        result.Items.Select(item => item.UploadId).ShouldBe([uploads.UploadedBeforeMidnight.Id]);
    }

    [Test]
    public async Task SearchUploadsAsync_ReleaseGroupFilter_ReturnsUploadsOfReleasesInGroup()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(ReleaseGroupId: uploads.SecondReleaseGroupId),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(2);
        result
            .Items.Select(item => item.UploadId)
            .ShouldBe([uploads.PendingUpload.Id, uploads.OfflineUpload.Id]);
    }

    [Test]
    public async Task SearchUploadsAsync_SearchTermMatchesReleaseNameInDifferentCase_ReturnsUploadsOfRelease()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(SearchTerm: "  bearcat.SECOND  "),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(2);
        result
            .Items.Select(item => item.UploadId)
            .ShouldBe([uploads.PendingUpload.Id, uploads.OfflineUpload.Id]);
    }

    [Test]
    public async Task SearchUploadsAsync_SearchTermMatchesHosterFileLinkWithNonAsciiCharacterInDifferentCase_ReturnsUpload()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(SearchTerm: "übersicht.PART2"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Select(item => item.UploadId).ShouldBe([uploads.OfflineUpload.Id]);
    }

    [Test]
    public async Task SearchUploadsAsync_SearchTermIsUploadIdWithHashPrefix_ReturnsUpload()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(SearchTerm: $"#{uploads.PendingUpload.Id}"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Select(item => item.UploadId).ShouldBe([uploads.PendingUpload.Id]);
    }

    [Test]
    public async Task SearchUploadsAsync_SearchTermIsUploadIdWithoutHashPrefix_ContainsUpload()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(SearchTerm: uploads.LatestUploadOfSecondHoster.Id.ToString()),
            CancellationToken.None
        );

        // Assert
        result
            .Items.Select(item => item.UploadId)
            .ShouldContain(uploads.LatestUploadOfSecondHoster.Id);
    }

    [Test]
    public async Task SearchUploadsAsync_SearchTermMatchesNothing_ReturnsEmptyResult()
    {
        // Arrange
        await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(SearchTerm: "does-not-exist"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task SearchUploadsAsync_SearchTermAndFilters_ReturnsUploadsMatchingAll()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(
                SearchTerm: "bearcat.first",
                HosterRegistrationId: uploads.SecondHosterRegistrationId,
                ReleaseGroupId: uploads.FirstReleaseGroupId
            ),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result
            .Items.Select(item => item.UploadId)
            .ShouldBe([uploads.LatestUploadOfSecondHoster.Id]);
    }

    [Test]
    public async Task SearchUploadsAsync_UploadsWithAndWithoutArchive_ProjectsArchiveId()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.SearchUploadsAsync(
            new UploadSearchQuery(ReleaseId: uploads.SecondReleaseId),
            CancellationToken.None
        );

        // Assert
        result
            .Items.Single(item => item.UploadId == uploads.OfflineUpload.Id)
            .ArchiveId.ShouldBe(uploads.SecondArchiveId);
        result
            .Items.Single(item => item.UploadId == uploads.PendingUpload.Id)
            .ArchiveId.ShouldBeNull();
    }

    [Test]
    public async Task GetUploadAsync_UploadExists_ReturnsProjectedReadModel()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.GetUploadAsync(
            uploads.OfflineUpload.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldNotBeNull();
        result.UploadId.ShouldBe(uploads.OfflineUpload.Id);
        result.ReleaseId.ShouldBe(uploads.SecondReleaseId);
        result.ReleaseName.ShouldBe("Bearcat.Second.2026-GRP");
        result.UploadConfigId.ShouldBe(uploads.OfflineUpload.UploadConfigId);
        result.UploadConfigName.ShouldBe("Second release upload");
        result.HosterRegistrationId.ShouldBe(uploads.SecondHosterRegistrationId);
        result.HosterRegistrationName.ShouldBe("Second hoster");
        result.CreatedAt.ShouldBe(Midnight.AddTicks(1000));
        result.UploadedAt.ShouldBe(Midnight.AddMilliseconds(500));
        result.UploadState.ShouldBe(UploadState.Completed);
        result.OnlineState.ShouldBe(OnlineState.Offline);
        result.NotFullyOnlineSince.ShouldBe(Midnight.AddHours(1).AddMilliseconds(250));
        result.FullyOfflineSince.ShouldBe(Midnight.AddHours(2).AddMilliseconds(750));
        result.LinkCount.ShouldBe(2);
        result.ErrorMessages.ShouldBe(["File offline", "Check of Façade.rar timed out"]);
        result.ArchiveId.ShouldBe(uploads.SecondArchiveId);
    }

    [Test]
    public async Task GetUploadAsync_UploadDoesNotExist_ReturnsNull()
    {
        // Arrange
        var uploads = await AddSearchUploadsAsync();

        // Act
        var result = await repository.GetUploadAsync(
            uploads.LatestUploadOfSecondHoster.Id + 1,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
    }

    private async Task<SearchUploads> AddSearchUploadsAsync()
    {
        var firstHoster = CreateHosterRegistration("First hoster");
        var secondHoster = CreateHosterRegistration("Second hoster");
        var firstRelease = CreateRelease("Bearcat.First.2026-GRP");
        var secondRelease = CreateRelease("Bearcat.Second.2026-GRP");
        var firstArchiveConfig = CreateArchiveConfig(firstRelease);
        var secondArchiveConfig = CreateArchiveConfig(secondRelease);
        var firstReleaseFirstHosterConfig = CreateUploadConfig(
            firstRelease,
            firstArchiveConfig,
            firstHoster,
            "First release upload"
        );
        var firstReleaseSecondHosterConfig = CreateUploadConfig(
            firstRelease,
            firstArchiveConfig,
            secondHoster,
            "First release mirror upload"
        );
        var secondReleaseConfig = CreateUploadConfig(
            secondRelease,
            secondArchiveConfig,
            secondHoster,
            "Second release upload"
        );
        var secondArchive = new Archive
        {
            ArchiveConfig = secondArchiveConfig,
            ArchiveFolderPath = "/tmp/archives/second",
            CreatedAt = Midnight,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 100,
        };

        DbContext.AddRange(
            firstReleaseFirstHosterConfig,
            firstReleaseSecondHosterConfig,
            secondReleaseConfig,
            secondArchive
        );
        await DbContext.SaveChangesAsync();

        var uploadedBeforeMidnight = await AddUploadAsync(
            firstReleaseFirstHosterConfig,
            createdAt: Midnight.AddMilliseconds(-100),
            uploadedAt: Midnight.AddTicks(-1000),
            UploadState.Completed,
            OnlineState.Online
        );
        var uploadedAtMidnight = await AddUploadAsync(
            firstReleaseFirstHosterConfig,
            createdAt: Midnight,
            uploadedAt: Midnight,
            UploadState.Completed,
            OnlineState.Online
        );
        var offlineUpload = await AddUploadAsync(
            secondReleaseConfig,
            createdAt: Midnight.AddTicks(1000),
            uploadedAt: Midnight.AddMilliseconds(500),
            UploadState.Completed,
            OnlineState.Offline,
            upload =>
            {
                upload.Archive = secondArchive;
                upload.NotFullyOnlineSince = Midnight.AddHours(1).AddMilliseconds(250);
                upload.FullyOfflineSince = Midnight.AddHours(2).AddMilliseconds(750);
                upload.ErrorMessages = ["File offline", "Check of Façade.rar timed out"];
                upload.UploadedFiles =
                [
                    CreateUploadedFile(secondArchive, "archive.part1.rar"),
                    CreateUploadedFile(secondArchive, "ÜBERSICHT.part2.rar"),
                ];
            }
        );
        var uploadedJustAfterMidnight = await AddUploadAsync(
            firstReleaseFirstHosterConfig,
            createdAt: Midnight.AddMilliseconds(250),
            uploadedAt: Midnight.AddTicks(1000),
            UploadState.Completed,
            OnlineState.Online
        );
        var pendingUpload = await AddUploadAsync(
            secondReleaseConfig,
            createdAt: Midnight.AddSeconds(1),
            uploadedAt: null,
            UploadState.Pending,
            OnlineState.Unknown
        );
        var latestUploadOfSecondHoster = await AddUploadAsync(
            firstReleaseSecondHosterConfig,
            createdAt: Midnight.AddSeconds(2),
            uploadedAt: Midnight.AddSeconds(1),
            UploadState.Completed,
            OnlineState.Online
        );
        DbContext.ChangeTracker.Clear();

        return new SearchUploads(
            firstRelease.Id,
            secondRelease.Id,
            firstRelease.ReleaseGroupId,
            secondRelease.ReleaseGroupId,
            secondArchive.Id,
            firstHoster.Id,
            secondHoster.Id,
            uploadedBeforeMidnight,
            uploadedAtMidnight,
            offlineUpload,
            uploadedJustAfterMidnight,
            pendingUpload,
            latestUploadOfSecondHoster
        );
    }

    private async Task<Upload> AddUploadAsync(
        UploadConfig uploadConfig,
        DateTime createdAt,
        DateTime? uploadedAt,
        UploadState uploadState,
        OnlineState onlineState,
        Action<Upload>? configure = null
    )
    {
        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            CreatedAt = createdAt,
            UploadedAt = uploadedAt,
            UploadState = uploadState,
            OnlineState = onlineState,
            UploadedFiles = [],
            ErrorMessages = [],
        };
        configure?.Invoke(upload);

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();

        return upload;
    }

    private static UploadedFile CreateUploadedFile(Archive archive, string fileName)
    {
        return new UploadedFile
        {
            ArchiveFile = new ArchiveFile { Archive = archive, FullFileName = fileName },
            HosterFileLink = $"https://hoster.test/{fileName}",
            OnlineState = OnlineState.Offline,
            CreatedAt = Midnight,
        };
    }

    private static HosterRegistration CreateHosterRegistration(string name)
    {
        return new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
    }

    private static Release CreateRelease(string name)
    {
        return new Release
        {
            Name = name,
            CreatedAt = Midnight,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };
    }

    private static ArchiveConfig CreateArchiveConfig(Release release)
    {
        return new ArchiveConfig
        {
            Release = release,
            Name = $"{release.Name} archive",
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "RarArchiver",
            ArchiveFileSizeMb = 100,
        };
    }

    private static UploadConfig CreateUploadConfig(
        Release release,
        ArchiveConfig archiveConfig,
        HosterRegistration hosterRegistration,
        string name
    )
    {
        return new UploadConfig
        {
            Release = release,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            Name = name,
        };
    }

    private sealed record SearchUploads(
        int FirstReleaseId,
        int SecondReleaseId,
        int FirstReleaseGroupId,
        int SecondReleaseGroupId,
        int SecondArchiveId,
        int FirstHosterRegistrationId,
        int SecondHosterRegistrationId,
        Upload UploadedBeforeMidnight,
        Upload UploadedAtMidnight,
        Upload OfflineUpload,
        Upload UploadedJustAfterMidnight,
        Upload PendingUpload,
        Upload LatestUploadOfSecondHoster
    );
}
