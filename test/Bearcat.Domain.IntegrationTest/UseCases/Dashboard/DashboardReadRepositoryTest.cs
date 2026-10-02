using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.Dashboard.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.Dashboard;

public class DashboardReadRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private DashboardReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        var readDbContext = CreateDbContext();
        readDbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        repository = new DashboardReadRepository(readDbContext);
    }

    [Test]
    public async Task GetUploadsPerDayAsync_NoFilter_GroupsUploadsByDayAndHoster()
    {
        // Arrange
        var alphaHoster = CreateHosterRegistration("Alpha");
        var betaHoster = CreateHosterRegistration("Beta");
        var release = AddRelease("Bearcat.Release.001");
        var alphaUploadConfig = AddUploadConfig(release, alphaHoster);
        var betaUploadConfig = AddUploadConfig(release, betaHoster);
        AddUpload(alphaUploadConfig, new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc));
        AddUpload(alphaUploadConfig, new DateTime(2026, 9, 3, 23, 59, 59, 999, DateTimeKind.Utc));
        AddUpload(alphaUploadConfig, new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc));
        AddUpload(betaUploadConfig, new DateTime(2026, 9, 3, 12, 30, 15, 250, DateTimeKind.Utc));
        AddUpload(betaUploadConfig, new DateTime(2026, 9, 5, 8, 0, 0, DateTimeKind.Utc));
        AddUpload(betaUploadConfig, uploadedAt: null, uploadState: UploadState.Pending);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetUploadsPerDayAsync(
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new UploadDayReadModel(new DateOnly(2026, 9, 3), "Alpha", 2),
            new UploadDayReadModel(new DateOnly(2026, 9, 3), "Beta", 1),
            new UploadDayReadModel(new DateOnly(2026, 9, 4), "Alpha", 1),
            new UploadDayReadModel(new DateOnly(2026, 9, 5), "Beta", 1),
        ]);
    }

    [Test]
    public async Task GetUploadsPerDayAsync_FromAndToFilter_IncludesWholeBoundaryDaysOnly()
    {
        // Arrange
        var hoster = CreateHosterRegistration("Alpha");
        var release = AddRelease("Bearcat.Release.001");
        var uploadConfig = AddUploadConfig(release, hoster);
        AddUpload(
            uploadConfig,
            new DateTime(2026, 9, 3, 23, 59, 59, 999, DateTimeKind.Utc).AddTicks(9000)
        );
        AddUpload(uploadConfig, new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc));
        AddUpload(uploadConfig, new DateTime(2026, 9, 4, 18, 0, 0, 500, DateTimeKind.Utc));
        AddUpload(
            uploadConfig,
            new DateTime(2026, 9, 5, 23, 59, 59, 999, DateTimeKind.Utc).AddTicks(9000)
        );
        AddUpload(uploadConfig, new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetUploadsPerDayAsync(
            uploadedFrom: new DateOnly(2026, 9, 4),
            uploadedTo: new DateOnly(2026, 9, 5),
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new UploadDayReadModel(new DateOnly(2026, 9, 4), "Alpha", 2),
            new UploadDayReadModel(new DateOnly(2026, 9, 5), "Alpha", 1),
        ]);
    }

    [Test]
    public async Task GetUploadsPerDayAsync_OnlyFromFilter_ExcludesUploadsBeforeMidnightOfFromDay()
    {
        // Arrange
        var hoster = CreateHosterRegistration("Alpha");
        var release = AddRelease("Bearcat.Release.001");
        var uploadConfig = AddUploadConfig(release, hoster);
        AddUpload(uploadConfig, new DateTime(2026, 9, 3, 23, 59, 59, 999, DateTimeKind.Utc));
        AddUpload(uploadConfig, new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc));
        AddUpload(uploadConfig, new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetUploadsPerDayAsync(
            uploadedFrom: new DateOnly(2026, 9, 4),
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new UploadDayReadModel(new DateOnly(2026, 9, 4), "Alpha", 1),
            new UploadDayReadModel(new DateOnly(2026, 10, 1), "Alpha", 1),
        ]);
    }

    [Test]
    public async Task GetUploadsPerDayAsync_OnlyToFilter_IncludesUploadsUntilEndOfToDay()
    {
        // Arrange
        var hoster = CreateHosterRegistration("Alpha");
        var release = AddRelease("Bearcat.Release.001");
        var uploadConfig = AddUploadConfig(release, hoster);
        AddUpload(uploadConfig, new DateTime(2026, 8, 30, 6, 0, 0, DateTimeKind.Utc));
        AddUpload(uploadConfig, new DateTime(2026, 9, 3, 23, 59, 59, 999, DateTimeKind.Utc));
        AddUpload(uploadConfig, new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetUploadsPerDayAsync(
            uploadedTo: new DateOnly(2026, 9, 3),
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new UploadDayReadModel(new DateOnly(2026, 8, 30), "Alpha", 1),
            new UploadDayReadModel(new DateOnly(2026, 9, 3), "Alpha", 1),
        ]);
    }

    [Test]
    public async Task GetReleaseOnlineStateSummaryAsync_ReleasesWithDifferentUploadStates_CountsReleasesPerOnlineState()
    {
        // Arrange
        var hoster = CreateHosterRegistration("Alpha");
        AddRelease("Bearcat.Release.Unknown");

        var onlineRelease = AddRelease("Bearcat.Release.Online");
        var onlineFirstUploadConfig = AddUploadConfig(onlineRelease, hoster);
        var onlineSecondUploadConfig = AddUploadConfig(onlineRelease, hoster);
        AddUpload(onlineFirstUploadConfig, DateTime.UtcNow, onlineState: OnlineState.Online);
        AddUpload(onlineFirstUploadConfig, DateTime.UtcNow, onlineState: OnlineState.Online);
        AddUpload(onlineSecondUploadConfig, DateTime.UtcNow, onlineState: OnlineState.Online);

        var secondOnlineRelease = AddRelease("Bearcat.Release.Online.Second");
        var secondOnlineUploadConfig = AddUploadConfig(secondOnlineRelease, hoster);
        AddUpload(secondOnlineUploadConfig, DateTime.UtcNow, onlineState: OnlineState.Offline);
        AddUpload(secondOnlineUploadConfig, DateTime.UtcNow, onlineState: OnlineState.Online);

        var partiallyOnlineRelease = AddRelease("Bearcat.Release.Partially");
        var partiallyOnlineFirstUploadConfig = AddUploadConfig(partiallyOnlineRelease, hoster);
        var partiallyOnlineSecondUploadConfig = AddUploadConfig(partiallyOnlineRelease, hoster);
        AddUpload(
            partiallyOnlineFirstUploadConfig,
            DateTime.UtcNow,
            onlineState: OnlineState.Online
        );
        AddUpload(
            partiallyOnlineSecondUploadConfig,
            DateTime.UtcNow,
            onlineState: OnlineState.Offline
        );

        var offlineRelease = AddRelease("Bearcat.Release.Offline");
        var offlineUploadConfig = AddUploadConfig(offlineRelease, hoster);
        AddUploadConfig(offlineRelease, hoster);
        AddUpload(offlineUploadConfig, DateTime.UtcNow, onlineState: OnlineState.Offline);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetReleaseOnlineStateSummaryAsync(CancellationToken.None);

        // Assert
        result.TotalReleaseCount.ShouldBe(5);
        result.Counts.ShouldBe([
            new ReleaseOnlineStateCountReadModel(OnlineState.Unknown, 1),
            new ReleaseOnlineStateCountReadModel(OnlineState.Online, 2),
            new ReleaseOnlineStateCountReadModel(OnlineState.PartiallyOnline, 1),
            new ReleaseOnlineStateCountReadModel(OnlineState.Offline, 1),
        ]);
    }

    private static HosterRegistration CreateHosterRegistration(string name)
    {
        return new HosterRegistration
        {
            Name = name,
            SerializedConfig = "{}",
            HosterClassName = $"{name}Hoster",
            IsActive = true,
        };
    }

    private Release AddRelease(string name)
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = new ReleaseGroup
            {
                Name = $"{name} group",
                EnableAutomaticReuploads = false,
                NumberOfHoursUntilReupload = 24,
            },
        };

        DbContext.Releases.Add(release);

        return release;
    }

    private UploadConfig AddUploadConfig(Release release, HosterRegistration hosterRegistration)
    {
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = new ArchiveConfig
            {
                Release = release,
                Name = "Main archive",
                ArchiveFilesBasePath = "/tmp/archive",
                ArchiverName = "zip",
                ArchiveFileSizeMb = 512,
            },
            HosterRegistration = hosterRegistration,
            Name = $"{hosterRegistration.Name} upload",
        };

        DbContext.UploadConfigs.Add(uploadConfig);

        return uploadConfig;
    }

    private void AddUpload(
        UploadConfig uploadConfig,
        DateTime? uploadedAt,
        UploadState uploadState = UploadState.Completed,
        OnlineState onlineState = OnlineState.Unknown
    )
    {
        DbContext.Uploads.Add(
            new Upload
            {
                UploadConfig = uploadConfig,
                CreatedAt = uploadedAt ?? DateTime.UtcNow,
                UploadedAt = uploadedAt,
                UploadState = uploadState,
                OnlineState = onlineState,
            }
        );
    }
}
