using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories.ReleaseRead;

public class ReleaseReadRepositorySearchResultTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime MidnightUtc = ReleaseReadRepositoryTestData.MidnightUtc;

    private ReleaseReadRepositoryTestData testData = null!;
    private ReleaseReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        testData = new ReleaseReadRepositoryTestData(DbContext);
        repository = new ReleaseReadRepository(
            ReadDbContext,
            Mock.Of<IArchiverFactory>(),
            Mock.Of<ILinkCrypterFactory>(),
            Mock.Of<IHosterFactory>()
        );
    }

    [Test]
    public async Task SearchReleaseResultsAsync_ReleaseWithMetadataAndClassification_MapsAllFields()
    {
        // Arrange
        var release = testData.AddRelease(
            "Bearcat.Movie.2026.1080p.WEB-DL-GRP",
            ReleaseContentType.Movie,
            releaseFolderPath: "/data/releases/Bearcat.Movie"
        );
        release.PrimaryLanguageCode = "de";
        release.CreatedAt = MidnightUtc.AddDays(2);
        release.UploadsPostedAt = MidnightUtc.AddDays(3);
        release.Metadata = new ReleaseMetadata
        {
            MetadataDatabaseClassName = "XrelNfoDatabase",
            Title = "Bearcat Movie",
            CoverUrl = "https://covers.example/bearcat.jpg",
        };
        release.Classification = new ReleaseClassification
        {
            Title = "Bearcat Movie",
            Year = 2026,
            ContentType = ReleaseContentType.Movie,
            Resolution = ReleaseResolution.R1080p,
            Source = ReleaseSource.WebDl,
            ClassifiedAt = MidnightUtc,
        };
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        var item = result.Items.Single();
        item.ReleaseId.ShouldBe(release.Id);
        item.Name.ShouldBe("Bearcat.Movie.2026.1080p.WEB-DL-GRP");
        item.ReleaseType.ShouldBe(ReleaseType.Managed);
        item.ReleaseContentType.ShouldBe(ReleaseContentType.Movie);
        item.PrimaryLanguageCode.ShouldBe("de");
        item.ReleaseGroupId.ShouldBe(release.ReleaseGroupId);
        item.ReleaseGroupName.ShouldBe("Bearcat.Movie.2026.1080p.WEB-DL-GRP group");
        item.ReleaseFolderPath.ShouldBe("/data/releases/Bearcat.Movie");
        item.CreatedAt.ShouldBe(MidnightUtc.AddDays(2));
        item.UploadsPostedAt.ShouldBe(MidnightUtc.AddDays(3));
        item.MetadataTitle.ShouldBe("Bearcat Movie");
        item.CoverUrl.ShouldBe("https://covers.example/bearcat.jpg");
        item.Year.ShouldBe(2026);
        item.Resolution.ShouldBe(ReleaseResolution.R1080p);
        item.Source.ShouldBe(ReleaseSource.WebDl);
        item.UploadConfigs.ShouldBeEmpty();
        item.OnlineState.ShouldBeNull();
    }

    [Test]
    public async Task SearchReleaseResultsAsync_ReleaseCreatedBeforeCreatedAtExisted_ReturnsNullCreatedAt()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Legacy.2020-GRP");
        release.CreatedAt = DateTime.MinValue;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(),
            CancellationToken.None
        );

        // Assert
        result.Items.Single().CreatedAt.ShouldBeNull();
    }

    [Test]
    public async Task SearchReleaseResultsAsync_ClassificationWithUnknownResolutionAndSource_ReturnsNullResolutionAndSource()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Unknown.2026-GRP");
        release.Classification = new ReleaseClassification
        {
            Title = "Bearcat Unknown",
            Year = 2025,
            ContentType = ReleaseContentType.Movie,
            Resolution = ReleaseResolution.Unknown,
            Source = ReleaseSource.Unknown,
            ClassifiedAt = MidnightUtc,
        };
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(),
            CancellationToken.None
        );

        // Assert
        var item = result.Items.Single();
        item.Year.ShouldBe(2025);
        item.Resolution.ShouldBeNull();
        item.Source.ShouldBeNull();
    }

    [Test]
    public async Task SearchReleaseResultsAsync_SeveralUploadConfigs_ReturnsThemOrderedByHosterNameThenIdWithOnlineStates()
    {
        // Arrange
        var alphaHoster = testData.AddHosterRegistration("Alpha hoster");
        var betaHoster = testData.AddHosterRegistration("Beta hoster");
        var release = testData.AddRelease("Bearcat.Configs.2026-GRP");
        var betaUploadConfig = testData.AddUploadConfig(release, "Beta upload", betaHoster);
        testData.AddUpload(
            betaUploadConfig,
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.Offline
        );
        testData.AddUpload(betaUploadConfig, MidnightUtc.AddHours(1), MidnightUtc.AddHours(1));
        await DbContext.SaveChangesAsync();
        var firstAlphaUploadConfig = testData.AddUploadConfig(
            release,
            "Alpha upload 1",
            alphaHoster
        );
        testData.AddUpload(
            firstAlphaUploadConfig,
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.Offline
        );
        await DbContext.SaveChangesAsync();
        var secondAlphaUploadConfig = testData.AddUploadConfig(
            release,
            "Alpha upload 2",
            alphaHoster
        );
        testData.AddUpload(
            secondAlphaUploadConfig,
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.PartiallyOnline
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(),
            CancellationToken.None
        );

        // Assert
        var item = result.Items.Single();
        item.UploadConfigs.ShouldBe([
            new ReleaseSearchResultUploadConfigReadModel(
                firstAlphaUploadConfig.Id,
                "Alpha hoster",
                OnlineState.Offline
            ),
            new ReleaseSearchResultUploadConfigReadModel(
                secondAlphaUploadConfig.Id,
                "Alpha hoster",
                OnlineState.PartiallyOnline
            ),
            new ReleaseSearchResultUploadConfigReadModel(
                betaUploadConfig.Id,
                "Beta hoster",
                OnlineState.Online
            ),
        ]);
        item.OnlineState.ShouldBe(OnlineState.PartiallyOnline);
    }

    [Test]
    public async Task SearchReleaseResultsAsync_ReleasesWithAndWithoutPendingPost_ReturnsReadyForPostQueueFlag()
    {
        // Arrange
        var readyRelease = testData.AddRelease("Bearcat.Ready.2026-GRP");
        testData.AddUpload(
            testData.AddUploadConfig(readyRelease, "Ready upload"),
            MidnightUtc,
            MidnightUtc
        );
        var postedRelease = testData.AddRelease("Bearcat.Posted.2026-GRP");
        postedRelease.UploadsPostedAt = MidnightUtc.AddHours(1);
        testData.AddUpload(
            testData.AddUploadConfig(postedRelease, "Posted upload"),
            MidnightUtc,
            MidnightUtc
        );
        var releaseWithoutUploadConfigs = testData.AddRelease("Bearcat.NoUploads.2026-GRP");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(),
            CancellationToken.None
        );

        // Assert
        var readyFlagsByReleaseId = result.Items.ToDictionary(
            item => item.ReleaseId,
            item => item.IsReadyForPostQueue
        );
        readyFlagsByReleaseId[readyRelease.Id].ShouldBeTrue();
        readyFlagsByReleaseId[postedRelease.Id].ShouldBeFalse();
        readyFlagsByReleaseId[releaseWithoutUploadConfigs.Id].ShouldBeFalse();
    }

    [Test]
    public async Task SearchReleaseResultsAsync_CreatedAtDescendingWithEqualCreatedAt_OrdersByIdDescending()
    {
        // Arrange
        var firstRelease = testData.AddRelease("Bearcat.A.2026-GRP");
        await DbContext.SaveChangesAsync();
        var secondRelease = testData.AddRelease("Bearcat.B.2026-GRP");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(SortOrder: ReleaseSearchSortOrder.CreatedAtDescending),
            CancellationToken.None
        );

        // Assert
        result.Items.Select(item => item.ReleaseId).ShouldBe([secondRelease.Id, firstRelease.Id]);
    }

    [Test]
    public async Task SearchReleaseResultsAsync_UploadsPostedAtDescending_PutsNotPostedReleasesLastAndOrdersTiesByName()
    {
        // Arrange
        testData.AddRelease("Bearcat.A.NotPosted.2026-GRP");
        var laterPostedRelease = testData.AddRelease("Bearcat.Z.Posted.2026-GRP");
        laterPostedRelease.UploadsPostedAt = MidnightUtc.AddDays(2);
        var earlierPostedRelease = testData.AddRelease("Bearcat.B.Posted.2026-GRP");
        earlierPostedRelease.UploadsPostedAt = MidnightUtc.AddDays(1);
        var laterPostedTieRelease = testData.AddRelease("Bearcat.Y.Posted.2026-GRP");
        laterPostedTieRelease.UploadsPostedAt = MidnightUtc.AddDays(2);
        testData.AddRelease("Bearcat.C.NotPosted.2026-GRP");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(SortOrder: ReleaseSearchSortOrder.UploadsPostedAtDescending),
            CancellationToken.None
        );

        // Assert
        result
            .Items.Select(item => item.Name)
            .ShouldBe([
                "Bearcat.Y.Posted.2026-GRP",
                "Bearcat.Z.Posted.2026-GRP",
                "Bearcat.B.Posted.2026-GRP",
                "Bearcat.A.NotPosted.2026-GRP",
                "Bearcat.C.NotPosted.2026-GRP",
            ]);
    }

    [Test]
    public async Task SearchReleaseResultsAsync_OfflineUploadConfigCountDescending_CountsConfigsWithoutOnlineUploadAndOrdersTiesByName()
    {
        // Arrange
        var mixedRelease = testData.AddRelease("Bearcat.B.Mixed.2026-GRP");
        var recoveredUploadConfig = testData.AddUploadConfig(mixedRelease, "B recovered");
        testData.AddUpload(
            recoveredUploadConfig,
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.Offline
        );
        testData.AddUpload(recoveredUploadConfig, MidnightUtc.AddHours(1), MidnightUtc.AddHours(1));
        testData.AddUploadConfig(mixedRelease, "B without upload");
        var offlineRelease = testData.AddRelease("Bearcat.A.Offline.2026-GRP");
        testData.AddUpload(
            testData.AddUploadConfig(offlineRelease, "A offline"),
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.Offline
        );
        var twoOfflineRelease = testData.AddRelease("Bearcat.Z.TwoOffline.2026-GRP");
        testData.AddUploadConfig(twoOfflineRelease, "Z without upload 1");
        testData.AddUploadConfig(twoOfflineRelease, "Z without upload 2");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(
                SortOrder: ReleaseSearchSortOrder.OfflineUploadConfigCountDescending
            ),
            CancellationToken.None
        );

        // Assert
        result
            .Items.Select(item => item.Name)
            .ShouldBe([
                "Bearcat.Z.TwoOffline.2026-GRP",
                "Bearcat.A.Offline.2026-GRP",
                "Bearcat.B.Mixed.2026-GRP",
            ]);
    }

    [Test]
    public async Task CountReleasesByOnlineStateAsync_ReleasesInEveryState_ReturnsCountPerState()
    {
        // Arrange
        testData.AddOnlineStateReleases();
        testData.AddUpload(
            testData.AddUploadConfig(
                testData.AddRelease("Bearcat.SecondOffline.2026-GRP"),
                "Second offline"
            ),
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.Offline
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.CountReleasesByOnlineStateAsync(
            new ReleaseSearchQuery(),
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new ReleaseOnlineStateCounts(
                TotalCount: 5,
                OnlineCount: 1,
                PartiallyOnlineCount: 1,
                OfflineCount: 2,
                WithoutUploadConfigsCount: 1
            )
        );
    }

    [Test]
    public async Task CountReleasesByOnlineStateAsync_OnlineStateInQuery_IgnoresOnlineStateFilter()
    {
        // Arrange
        testData.AddOnlineStateReleases();
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.CountReleasesByOnlineStateAsync(
            new ReleaseSearchQuery(
                OnlineState: OnlineState.Online,
                PageIndex: 3,
                PageSize: 5,
                SortOrder: ReleaseSearchSortOrder.CreatedAtDescending
            ),
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new ReleaseOnlineStateCounts(
                TotalCount: 4,
                OnlineCount: 1,
                PartiallyOnlineCount: 1,
                OfflineCount: 1,
                WithoutUploadConfigsCount: 1
            )
        );
    }
}
