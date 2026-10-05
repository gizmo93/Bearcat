using Bearcat.Abstractions.Archiver;
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
            Mock.Of<ILinkCrypterFactory>()
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
    public async Task SearchReleaseResultsAsync_ReleaseWithoutMetadataAndClassification_ReturnsNullForTheirFields()
    {
        // Arrange
        testData.AddRelease("Bearcat.Plain.2026-GRP");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(),
            CancellationToken.None
        );

        // Assert
        var item = result.Items.Single();
        item.MetadataTitle.ShouldBeNull();
        item.CoverUrl.ShouldBeNull();
        item.Year.ShouldBeNull();
        item.Resolution.ShouldBeNull();
        item.Source.ShouldBeNull();
        item.UploadsPostedAt.ShouldBeNull();
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
    public async Task SearchReleaseResultsAsync_SeveralUploadConfigs_ReturnsThemOrderedByHosterNameThenIdWithOnlineFlags()
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
                false
            ),
            new ReleaseSearchResultUploadConfigReadModel(
                secondAlphaUploadConfig.Id,
                "Alpha hoster",
                false
            ),
            new ReleaseSearchResultUploadConfigReadModel(betaUploadConfig.Id, "Beta hoster", true),
        ]);
        item.OnlineState.ShouldBe(OnlineState.PartiallyOnline);
    }

    [TestCase("Bearcat.NoUploadConfigs.2026-GRP", null)]
    [TestCase("Bearcat.AllOnline.2026-GRP", OnlineState.Online)]
    [TestCase("Bearcat.PartiallyOnline.2026-GRP", OnlineState.PartiallyOnline)]
    [TestCase("Bearcat.Offline.2026-GRP", OnlineState.Offline)]
    public async Task SearchReleaseResultsAsync_ReleaseWithUploadConfigs_ComputesOnlineStateFromUploadConfigs(
        string releaseName,
        OnlineState? expectedOnlineState
    )
    {
        // Arrange
        testData.AddOnlineStateReleases();
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(),
            CancellationToken.None
        );

        // Assert
        result
            .Items.Single(item => item.Name == releaseName)
            .OnlineState.ShouldBe(expectedOnlineState);
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
    public async Task SearchReleaseResultsAsync_FilterAndSecondPage_ReturnsTotalCountOfFilterAndRemainingReleases()
    {
        // Arrange
        foreach (var number in new[] { 4, 7, 1, 6, 3, 2, 5 })
        {
            testData.AddRelease($"Bearcat.Page.0{number}.2026-GRP");
        }

        testData.AddRelease("Bearcat.S01E01.2026-GRP", ReleaseContentType.TvShowEpisode);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(
                ReleaseContentType: ReleaseContentType.Movie,
                PageIndex: 1,
                PageSize: 5
            ),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(7);
        result.PageIndex.ShouldBe(1);
        result.PageSize.ShouldBe(5);
        result
            .Items.Select(item => item.Name)
            .ShouldBe(["Bearcat.Page.06.2026-GRP", "Bearcat.Page.07.2026-GRP"]);
    }

    [TestCase(ReleaseSearchSortOrder.NameAscending, new[] { "A", "B", "C", "D" })]
    [TestCase(ReleaseSearchSortOrder.CreatedAtDescending, new[] { "B", "C", "A", "D" })]
    [TestCase(ReleaseSearchSortOrder.UploadsPostedAtDescending, new[] { "C", "B", "D", "A" })]
    [TestCase(
        ReleaseSearchSortOrder.OfflineUploadConfigCountDescending,
        new[] { "C", "A", "B", "D" }
    )]
    public async Task SearchReleaseResultsAsync_SortOrder_ReturnsReleasesInThatOrder(
        ReleaseSearchSortOrder sortOrder,
        string[] expectedReleaseNameParts
    )
    {
        // Arrange
        testData.AddSortOrderReleases();
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(SortOrder: sortOrder),
            CancellationToken.None
        );

        // Assert
        result
            .Items.Select(item => item.Name)
            .ShouldBe(expectedReleaseNameParts.Select(part => $"Bearcat.{part}.2026-GRP"));
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
    public async Task CountReleasesByOnlineStateAsync_NoReleases_ReturnsZeroCounts()
    {
        // Act
        var result = await repository.CountReleasesByOnlineStateAsync(
            new ReleaseSearchQuery(),
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(new ReleaseOnlineStateCounts(0, 0, 0, 0, 0));
    }

    [Test]
    public async Task CountReleasesByOnlineStateAsync_OtherFilters_CountsOnlyMatchingReleases()
    {
        // Arrange
        testData.AddOnlineStateReleases();
        testData.AddRelease("Bearcat.S01E01.2026-GRP", ReleaseContentType.TvShowEpisode);
        testData.AddUpload(
            testData.AddUploadConfig(
                testData.AddRelease("Bearcat.S01E02.2026-GRP", ReleaseContentType.TvShowEpisode),
                "Episode online"
            ),
            MidnightUtc,
            MidnightUtc
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.CountReleasesByOnlineStateAsync(
            new ReleaseSearchQuery(ReleaseContentType: ReleaseContentType.TvShowEpisode),
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new ReleaseOnlineStateCounts(
                TotalCount: 2,
                OnlineCount: 1,
                PartiallyOnlineCount: 0,
                OfflineCount: 0,
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

    [TestCase(OnlineState.Unknown)]
    [TestCase(OnlineState.Online)]
    [TestCase(OnlineState.PartiallyOnline)]
    [TestCase(OnlineState.Offline)]
    public async Task CountReleasesByOnlineStateAsync_EachState_EqualsTotalCountOfSearchFilteredByThatState(
        OnlineState onlineState
    )
    {
        // Arrange
        testData.AddOnlineStateReleases();
        testData.AddSortOrderReleases();
        testData.AddRelease("Bearcat.SecondNoUploadConfigs.2026-GRP");
        await DbContext.SaveChangesAsync();

        // Act
        var counts = await repository.CountReleasesByOnlineStateAsync(
            new ReleaseSearchQuery(),
            CancellationToken.None
        );
        var searchResult = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(OnlineState: onlineState),
            CancellationToken.None
        );

        // Assert
        var expectedCount = onlineState switch
        {
            OnlineState.Unknown => counts.WithoutUploadConfigsCount,
            OnlineState.Online => counts.OnlineCount,
            OnlineState.PartiallyOnline => counts.PartiallyOnlineCount,
            OnlineState.Offline => counts.OfflineCount,
            _ => throw new ArgumentOutOfRangeException(nameof(onlineState), onlineState, null),
        };
        searchResult.TotalCount.ShouldBe(expectedCount);
        searchResult.TotalCount.ShouldBeGreaterThan(0);
    }
}
