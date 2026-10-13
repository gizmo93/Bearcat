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

public class ReleaseReadRepositoryDownloadLinkSearchTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string FullLink = "https://hoster.example/files/abc123/Bearcat.part01.rar";

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
    public async Task SearchReleaseResultsAsync_FullLink_ReturnsOnlyReleaseWithExactlyThatLink()
    {
        // Arrange
        var matchingRelease = AddReleaseWithUploadedFile("Bearcat.Exact.2026-GRP", FullLink);
        AddReleaseWithUploadedFile("Bearcat.Longer.2026-GRP", FullLink + ".html");
        AddReleaseWithUploadedFile(
            "Bearcat.Other.2026-GRP",
            "https://hoster.example/files/def456/Other.part01.rar"
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(DownloadLink: FullLink),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(matchingRelease.Id);
    }

    [TestCase("http://hoster.example/files/abc123/Bearcat.part01.rar")]
    [TestCase("https://hoster.example/files/ABC123/Bearcat.part01.rar")]
    [TestCase("https://hoster.example/files/abc123")]
    public async Task SearchReleaseResultsAsync_FullLinkThatDiffersFromStoredLink_ReturnsNoRelease(
        string downloadLink
    )
    {
        // Arrange
        AddReleaseWithUploadedFile("Bearcat.Exact.2026-GRP", FullLink);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(DownloadLink: downloadLink),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task SearchReleaseResultsAsync_FullLinkWithSurroundingWhitespace_ReturnsReleaseWithThatLink()
    {
        // Arrange
        var matchingRelease = AddReleaseWithUploadedFile("Bearcat.Exact.2026-GRP", FullLink);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(DownloadLink: $"  {FullLink}\t "),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(matchingRelease.Id);
    }

    [Test]
    public async Task SearchReleaseResultsAsync_FullLinkWithUppercaseScheme_ReturnsOnlyReleaseWithExactlyThatLink()
    {
        // Arrange
        const string uppercaseSchemeLink = "HTTP://hoster.example/files/abc123";
        var matchingRelease = AddReleaseWithUploadedFile(
            "Bearcat.Exact.2026-GRP",
            uppercaseSchemeLink
        );
        AddReleaseWithUploadedFile("Bearcat.Longer.2026-GRP", uppercaseSchemeLink + "/part02");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(DownloadLink: uppercaseSchemeLink),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(matchingRelease.Id);
    }

    [Test]
    public async Task SearchReleaseResultsAsync_PartialLinkWithDifferentCase_ReturnsEachReleaseContainingItOnce()
    {
        // Arrange
        var releaseWithTwoMatchingFiles = AddReleaseWithUploadedFile(
            "Bearcat.A.2026-GRP",
            "https://hoster.example/files/AbC123/Bearcat.A.part01.rar"
        );
        var secondUpload = testData.AddUpload(
            testData.AddUploadConfig(releaseWithTwoMatchingFiles, "Second upload"),
            MidnightUtc,
            MidnightUtc
        );
        testData.AddUploadedFile(
            secondUpload,
            "Bearcat.A.2026-GRP.part02.rar",
            "https://mirror.example/abc123/Bearcat.A.part02.rar"
        );
        var releaseWithLowercaseLink = AddReleaseWithUploadedFile(
            "Bearcat.B.2026-GRP",
            "https://hoster.example/files/xyz/abc123.part01.rar"
        );
        AddReleaseWithUploadedFile(
            "Bearcat.C.2026-GRP",
            "https://hoster.example/files/def456/Bearcat.C.part01.rar"
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(
                DownloadLink: "aBC123",
                SortOrder: ReleaseSearchSortOrder.NameAscending
            ),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(2);
        result
            .Items.Select(item => item.ReleaseId)
            .ShouldBe([releaseWithTwoMatchingFiles.Id, releaseWithLowercaseLink.Id]);
    }

    [Test]
    public async Task SearchReleaseResultsAsync_LinkWithoutScheme_ReturnsReleasesContainingIt()
    {
        // Arrange
        var exactRelease = AddReleaseWithUploadedFile("Bearcat.A.Exact.2026-GRP", FullLink);
        var longerRelease = AddReleaseWithUploadedFile(
            "Bearcat.B.Longer.2026-GRP",
            FullLink + ".html"
        );
        AddReleaseWithUploadedFile(
            "Bearcat.C.Other.2026-GRP",
            "https://hoster.example/files/def456/Other.part01.rar"
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(
                DownloadLink: "hoster.example/files/abc123/Bearcat.part01.rar",
                SortOrder: ReleaseSearchSortOrder.NameAscending
            ),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(2);
        result.Items.Select(item => item.ReleaseId).ShouldBe([exactRelease.Id, longerRelease.Id]);
    }

    [Test]
    public async Task SearchReleaseResultsAsync_PartialLinkWithOtherFiltersAndPaging_ReturnsRequestedPageOfMatchingReleases()
    {
        // Arrange
        for (var movieNumber = 1; movieNumber <= 7; movieNumber++)
        {
            AddReleaseWithUploadedFile(
                $"Bearcat.Movie.{movieNumber:00}.2026-GRP",
                $"https://hoster.example/shared123/movie{movieNumber}.rar"
            );
        }

        AddReleaseWithUploadedFile(
            "Bearcat.Movie.08.2026-GRP",
            "https://hoster.example/other/movie8.rar"
        );
        AddReleaseWithUploadedFile(
            "Bearcat.S01E01.2026-GRP",
            "https://hoster.example/shared123/episode1.rar",
            ReleaseContentType.TvShowEpisode
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(
                ReleaseContentType: ReleaseContentType.Movie,
                DownloadLink: "SHARED123",
                SortOrder: ReleaseSearchSortOrder.NameAscending,
                PageIndex: 1,
                PageSize: 5
            ),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(7);
        result.PageIndex.ShouldBe(1);
        result
            .Items.Select(item => item.Name)
            .ShouldBe(["Bearcat.Movie.06.2026-GRP", "Bearcat.Movie.07.2026-GRP"]);
    }

    [Test]
    public async Task SearchReleaseResultsAsync_PartialLinkMatchingMoreReleasesThanSqliteVariablesPerStatement_ReturnsAllMatchingReleases()
    {
        // Arrange
        const int matchingReleaseCount = 1050;
        var hosterRegistration = testData.AddHosterRegistration("Shared hoster");
        for (var releaseNumber = 1; releaseNumber <= matchingReleaseCount; releaseNumber++)
        {
            var release = testData.AddRelease($"Bearcat.Broad.{releaseNumber:0000}.2026-GRP");
            var upload = testData.AddUpload(
                testData.AddUploadConfig(release, "Upload", hosterRegistration),
                MidnightUtc,
                MidnightUtc
            );
            testData.AddUploadedFile(
                upload,
                $"Bearcat.Broad.{releaseNumber:0000}.2026-GRP.part01.rar",
                $"https://hoster.example/files/broad{releaseNumber}.rar"
            );
        }

        AddReleaseWithUploadedFile("Bearcat.Other.2026-GRP", "https://other.example/files/x.rar");
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        await UpdatePostgresPlannerStatisticsAsync(ReadDbContext);
        await LimitSqliteVariableCountPerStatementAsync(ReadDbContext, maximumVariableCount: 1020);

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(
                DownloadLink: "hoster.example",
                SortOrder: ReleaseSearchSortOrder.NameAscending,
                PageIndex: 10,
                PageSize: 100
            ),
            CancellationToken.None
        );
        var counts = await repository.CountReleasesByOnlineStateAsync(
            new ReleaseSearchQuery(DownloadLink: "hoster.example"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(matchingReleaseCount);
        result
            .Items.Select(item => item.Name)
            .ShouldBe(
                Enumerable
                    .Range(1001, 50)
                    .Select(releaseNumber => $"Bearcat.Broad.{releaseNumber:0000}.2026-GRP")
            );
        counts.ShouldBe(
            new ReleaseOnlineStateCounts(
                TotalCount: matchingReleaseCount,
                OnlineCount: matchingReleaseCount,
                PartiallyOnlineCount: 0,
                OfflineCount: 0,
                WithoutUploadConfigsCount: 0
            )
        );
    }

    [Test]
    public async Task SearchReleaseResultsAsync_LinkWithoutMatch_ReturnsNoRelease()
    {
        // Arrange
        AddReleaseWithUploadedFile("Bearcat.Exact.2026-GRP", FullLink);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleaseResultsAsync(
            new ReleaseSearchQuery(DownloadLink: "does-not-exist"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task CountReleasesByOnlineStateAsync_PartialLink_CountsOnlyReleasesWithMatchingLink()
    {
        // Arrange
        AddReleaseWithUploadedFile("Bearcat.Online.1.2026-GRP", "https://hoster.example/abc123/1");
        AddReleaseWithUploadedFile("Bearcat.Online.2.2026-GRP", "https://hoster.example/ABC123/2");
        AddReleaseWithUploadedFile(
            "Bearcat.Offline.2026-GRP",
            "https://hoster.example/abc123/3",
            uploadOnlineState: OnlineState.Offline
        );
        AddReleaseWithUploadedFile("Bearcat.OtherLink.2026-GRP", "https://hoster.example/def456");
        testData.AddRelease("Bearcat.NoUploadConfigs.2026-GRP");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.CountReleasesByOnlineStateAsync(
            new ReleaseSearchQuery(DownloadLink: "abc123", OnlineState: OnlineState.Offline),
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new ReleaseOnlineStateCounts(
                TotalCount: 3,
                OnlineCount: 2,
                PartiallyOnlineCount: 0,
                OfflineCount: 1,
                WithoutUploadConfigsCount: 0
            )
        );
    }

    [Test]
    public async Task CountReleasesByOnlineStateAsync_FullLink_CountsOnlyReleaseWithExactlyThatLink()
    {
        // Arrange
        AddReleaseWithUploadedFile(
            "Bearcat.Exact.2026-GRP",
            FullLink,
            uploadOnlineState: OnlineState.Offline
        );
        AddReleaseWithUploadedFile("Bearcat.Longer.2026-GRP", FullLink + ".html");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.CountReleasesByOnlineStateAsync(
            new ReleaseSearchQuery(DownloadLink: FullLink),
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new ReleaseOnlineStateCounts(
                TotalCount: 1,
                OnlineCount: 0,
                PartiallyOnlineCount: 0,
                OfflineCount: 1,
                WithoutUploadConfigsCount: 0
            )
        );
    }

    private Release AddReleaseWithUploadedFile(
        string releaseName,
        string hosterFileLink,
        ReleaseContentType releaseContentType = ReleaseContentType.Movie,
        OnlineState uploadOnlineState = OnlineState.Online
    )
    {
        var release = testData.AddRelease(releaseName, releaseContentType);
        var upload = testData.AddUpload(
            testData.AddUploadConfig(release, "Upload"),
            MidnightUtc,
            MidnightUtc,
            onlineState: uploadOnlineState
        );
        testData.AddUploadedFile(upload, $"{releaseName}.part01.rar", hosterFileLink);

        return release;
    }
}
