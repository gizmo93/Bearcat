using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class ReleaseReadRepositorySearchTest(DatabaseProvider databaseProvider)
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
    public async Task SearchReleasesAsync_SearchTermWithDifferentCaseNonAsciiCharacters_ReturnsReleaseWithMatchingName()
    {
        // Arrange
        var matchingRelease = testData.AddRelease("Éclair.In.Paris.2026-GRP");
        testData.AddRelease("Bearcat.Other.2026-GRP");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(SearchTerm: " éCL "),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(matchingRelease.Id);
        result.Items.Single().Name.ShouldBe("Éclair.In.Paris.2026-GRP");
    }

    [Test]
    public async Task SearchReleasesAsync_SearchTermWithNonAsciiCharacters_MatchesNameOrReleaseFolderPath()
    {
        // Arrange
        var matchingRelease = testData.AddRelease(
            "Bearcat.Folder.2026-GRP",
            releaseFolderPath: "/data/Émigré/Bearcat.Folder.2026-GRP"
        );
        testData.AddRelease("Bearcat.Other.2026-GRP");
        testData.AddRelease("Émigré.Unmanaged.2026-GRP", releaseType: ReleaseType.Unmanaged);
        testData.AddRelease("Bearcat.Unmanaged.2026-GRP", releaseType: ReleaseType.Unmanaged);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(SearchTerm: "éMIGRÉ"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(2);
        result
            .Items.Select(item => item.Name)
            .ShouldBe(["Bearcat.Folder.2026-GRP", "Émigré.Unmanaged.2026-GRP"], ignoreOrder: true);
        result
            .Items.Single(item => item.ReleaseId == matchingRelease.Id)
            .ReleaseFolderPath.ShouldBe("/data/Émigré/Bearcat.Folder.2026-GRP");
    }

    [TestCase(OnlineState.Unknown, "Bearcat.NoUploadConfigs.2026-GRP")]
    [TestCase(OnlineState.Online, "Bearcat.AllOnline.2026-GRP")]
    [TestCase(OnlineState.PartiallyOnline, "Bearcat.PartiallyOnline.2026-GRP")]
    [TestCase(OnlineState.Offline, "Bearcat.Offline.2026-GRP")]
    public async Task SearchReleasesAsync_OnlineStateFilter_ReturnsReleaseWithThatOnlineState(
        OnlineState onlineState,
        string expectedReleaseName
    )
    {
        // Arrange
        AddOnlineStateReleases();
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(OnlineState: onlineState),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        var release = result.Items.Single();
        release.Name.ShouldBe(expectedReleaseName);
        release.OnlineState.ShouldBe(onlineState == OnlineState.Unknown ? null : onlineState);
    }

    [Test]
    public async Task SearchReleasesAsync_ConfigWithSeveralOnlineUploads_CountsOnlineUploadConfigOnce()
    {
        // Arrange
        AddOnlineStateReleases();
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(4);
        var countsByName = result.Items.ToDictionary(
            item => item.Name,
            item => (item.ActiveUploadConfigsCount, item.OnlineUploadConfigsCount)
        );
        countsByName["Bearcat.AllOnline.2026-GRP"].ShouldBe((2, 2));
        countsByName["Bearcat.NoUploadConfigs.2026-GRP"].ShouldBe((0, 0));
        countsByName["Bearcat.Offline.2026-GRP"].ShouldBe((2, 0));
        countsByName["Bearcat.PartiallyOnline.2026-GRP"].ShouldBe((3, 1));
    }

    [Test]
    public async Task SearchReleasesAsync_ReleaseContentTypeFilter_ReturnsMatchingRelease()
    {
        // Arrange
        testData.AddRelease("Bearcat.Movie.2026-GRP", ReleaseContentType.Movie);
        var episodeRelease = testData.AddRelease(
            "Bearcat.S01E01.2026-GRP",
            ReleaseContentType.TvShowEpisode
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(ReleaseContentType: ReleaseContentType.TvShowEpisode),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(episodeRelease.Id);
        result.Items.Single().ReleaseContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
    }

    [Test]
    public async Task SearchReleasesAsync_HosterRegistrationFilter_ReturnsReleaseUsingThatHoster()
    {
        // Arrange
        var searchedHoster = testData.AddHosterRegistration("Searched hoster");
        var otherHoster = testData.AddHosterRegistration("Other hoster");
        var matchingRelease = testData.AddRelease("Bearcat.Searched.2026-GRP");
        testData.AddUploadConfig(matchingRelease, "Searched upload", searchedHoster);
        testData.AddUploadConfig(matchingRelease, "Other upload", otherHoster);
        var otherRelease = testData.AddRelease("Bearcat.Other.2026-GRP");
        testData.AddUploadConfig(otherRelease, "Other upload", otherHoster);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(HosterRegistrationId: searchedHoster.Id),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(matchingRelease.Id);
    }

    [Test]
    public async Task SearchReleasesAsync_ArchiverNameFilter_ReturnsReleaseUsingThatArchiver()
    {
        // Arrange
        var matchingRelease = testData.AddRelease("Bearcat.SevenZip.2026-GRP");
        testData.AddArchiveConfig(matchingRelease, "RAR", "RarArchiver");
        testData.AddArchiveConfig(matchingRelease, "7z", "SevenZipArchiver");
        var otherRelease = testData.AddRelease("Bearcat.Rar.2026-GRP");
        testData.AddArchiveConfig(otherRelease, "RAR", "RarArchiver");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(ArchiverName: " SevenZipArchiver "),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(matchingRelease.Id);
    }

    [Test]
    public async Task SearchReleasesAsync_LinkCrypterRegistrationFilter_ReturnsReleaseUsingThatLinkCrypter()
    {
        // Arrange
        var searchedLinkCrypter = testData.AddLinkCrypterRegistration("Searched crypter");
        var otherLinkCrypter = testData.AddLinkCrypterRegistration("Other crypter");
        var matchingRelease = testData.AddRelease("Bearcat.Searched.2026-GRP");
        var matchingUploadConfig = testData.AddUploadConfig(matchingRelease, "Searched upload");
        testData.AddUploadConfigLinkCrypter(matchingUploadConfig, otherLinkCrypter);
        testData.AddUploadConfigLinkCrypter(matchingUploadConfig, searchedLinkCrypter);
        var otherRelease = testData.AddRelease("Bearcat.Other.2026-GRP");
        var otherUploadConfig = testData.AddUploadConfig(otherRelease, "Other upload");
        testData.AddUploadConfigLinkCrypter(otherUploadConfig, otherLinkCrypter);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(LinkCrypterRegistrationId: searchedLinkCrypter.Id),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(matchingRelease.Id);
    }

    [Test]
    public async Task SearchReleasesAsync_ReleaseGroupFilter_ReturnsReleaseOfThatGroup()
    {
        // Arrange
        testData.AddRelease("Bearcat.First.2026-GRP");
        var matchingRelease = testData.AddRelease("Bearcat.Second.2026-GRP");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(ReleaseGroupId: matchingRelease.ReleaseGroupId),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        var release = result.Items.Single();
        release.ReleaseId.ShouldBe(matchingRelease.Id);
        release.ReleaseGroupId.ShouldBe(matchingRelease.ReleaseGroupId);
        release.ReleaseGroupName.ShouldBe("Bearcat.Second.2026-GRP group");
    }

    [Test]
    public async Task SearchReleasesAsync_PostedLocationUrlWithDifferentCaseNonAsciiCharacters_ReturnsReleasePostedThere()
    {
        // Arrange
        var matchingRelease = testData.AddRelease("Bearcat.Posted.2026-GRP");
        matchingRelease.PostedLocations.Add(
            new PostedLocation
            {
                Url = "https://forum.example/threads/Café-near-Bearcat.123/",
                CreatedAt = MidnightUtc,
            }
        );
        var otherRelease = testData.AddRelease("Bearcat.Other.2026-GRP");
        otherRelease.PostedLocations.Add(
            new PostedLocation
            {
                Url = "https://forum.example/threads/other.456/",
                CreatedAt = MidnightUtc,
            }
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(PostedLocationUrl: "CAFÉ-NEAR"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(matchingRelease.Id);
    }

    [Test]
    public async Task SearchReleasesAsync_DownloadLinkWithDifferentCaseNonAsciiCharacters_ReturnsReleaseWithThatLink()
    {
        // Arrange
        var matchingRelease = testData.AddRelease("Bearcat.Link.2026-GRP");
        var matchingUpload = testData.AddUpload(
            testData.AddUploadConfig(matchingRelease, "Upload"),
            MidnightUtc,
            MidnightUtc
        );
        testData.AddUploadedFile(
            matchingUpload,
            "Bearcat.Link.2026-GRP.part01.rar",
            "https://hoster.example/files/Façade.part01.rar"
        );
        var otherRelease = testData.AddRelease("Bearcat.Other.2026-GRP");
        var otherUpload = testData.AddUpload(
            testData.AddUploadConfig(otherRelease, "Upload"),
            MidnightUtc,
            MidnightUtc
        );
        testData.AddUploadedFile(
            otherUpload,
            "Bearcat.Other.2026-GRP.part01.rar",
            "https://hoster.example/files/other.part01.rar"
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(DownloadLink: "FAÇADE.PART01"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(matchingRelease.Id);
    }

    [Test]
    public async Task SearchReleasesAsync_ArchiveFileNameWithDifferentCaseNonAsciiCharacters_ReturnsReleaseWithThatFile()
    {
        // Arrange
        var matchingRelease = testData.AddRelease("Bearcat.Archive.2026-GRP");
        var matchingUpload = testData.AddUpload(
            testData.AddUploadConfig(matchingRelease, "Upload"),
            MidnightUtc,
            MidnightUtc
        );
        testData.AddUploadedFile(
            matchingUpload,
            "Bearcat.Étude.2026-GRP.part01.rar",
            "https://hoster.example/files/abc"
        );
        var otherRelease = testData.AddRelease("Bearcat.Other.2026-GRP");
        var otherUpload = testData.AddUpload(
            testData.AddUploadConfig(otherRelease, "Upload"),
            MidnightUtc,
            MidnightUtc
        );
        testData.AddUploadedFile(
            otherUpload,
            "Bearcat.Other.2026-GRP.part01.rar",
            "https://hoster.example/files/def"
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(ArchiveFileName: "éTUDE"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(matchingRelease.Id);
    }

    [Test]
    public async Task SearchReleasesAsync_NonNumericUploadId_ReturnsNoReleases()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Upload.2026-GRP");
        testData.AddUpload(testData.AddUploadConfig(release, "Upload"), MidnightUtc, MidnightUtc);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(UploadId: "#abc"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task SearchReleasesAsync_SecondPage_ReturnsRemainingReleasesOrderedByName()
    {
        // Arrange
        foreach (var number in new[] { 4, 7, 1, 6, 3, 2, 5 })
        {
            testData.AddRelease($"Bearcat.Page.0{number}.2026-GRP");
        }

        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(PageIndex: 1, PageSize: 5),
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

    private void AddOnlineStateReleases()
    {
        testData.AddRelease("Bearcat.NoUploadConfigs.2026-GRP");

        var allOnlineRelease = testData.AddRelease("Bearcat.AllOnline.2026-GRP");
        var allOnlineFirstConfig = testData.AddUploadConfig(allOnlineRelease, "All online 1");
        testData.AddUpload(
            allOnlineFirstConfig,
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.Offline
        );
        testData.AddUpload(allOnlineFirstConfig, MidnightUtc.AddHours(1), MidnightUtc.AddHours(1));
        testData.AddUpload(
            testData.AddUploadConfig(allOnlineRelease, "All online 2"),
            MidnightUtc,
            MidnightUtc
        );

        var partiallyOnlineRelease = testData.AddRelease("Bearcat.PartiallyOnline.2026-GRP");
        var partiallyOnlineFirstConfig = testData.AddUploadConfig(
            partiallyOnlineRelease,
            "Partially online 1"
        );
        testData.AddUpload(partiallyOnlineFirstConfig, MidnightUtc, MidnightUtc);
        testData.AddUpload(
            partiallyOnlineFirstConfig,
            MidnightUtc.AddMilliseconds(500),
            MidnightUtc.AddMilliseconds(500)
        );
        testData.AddUpload(
            testData.AddUploadConfig(partiallyOnlineRelease, "Partially online 2"),
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.Offline
        );
        testData.AddUploadConfig(partiallyOnlineRelease, "Partially online 3");

        var offlineRelease = testData.AddRelease("Bearcat.Offline.2026-GRP");
        testData.AddUpload(
            testData.AddUploadConfig(offlineRelease, "Offline 1"),
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.Offline
        );
        testData.AddUpload(
            testData.AddUploadConfig(offlineRelease, "Offline 2"),
            MidnightUtc,
            null,
            UploadState.Failed,
            OnlineState.Unknown
        );
    }
}
