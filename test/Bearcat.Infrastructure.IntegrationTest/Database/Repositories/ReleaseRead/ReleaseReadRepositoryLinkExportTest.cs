using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.Hoster;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories.ReleaseRead;

public class ReleaseReadRepositoryLinkExportTest(DatabaseProvider databaseProvider)
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
    public async Task StreamLinksForExportAsync_FilterByUploadOnlineState_ReturnsAllLinksOfMatchingUploads()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Export.2026-GRP");
        var partiallyOnlineUpload = testData.AddUpload(
            testData.AddUploadConfig(release, "Partially online"),
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.PartiallyOnline
        );
        testData.AddUploadedFile(partiallyOnlineUpload, "a.part1.rar", "https://hoster/online");
        testData.AddUploadedFile(
            partiallyOnlineUpload,
            "a.part2.rar",
            "https://hoster/offline",
            OnlineState.Offline
        );
        var offlineUpload = testData.AddUpload(
            testData.AddUploadConfig(release, "Offline"),
            MidnightUtc,
            MidnightUtc,
            onlineState: OnlineState.Offline
        );
        testData.AddUploadedFile(
            offlineUpload,
            "b.part1.rar",
            "https://hoster/fully-offline",
            OnlineState.Offline
        );
        await DbContext.SaveChangesAsync();

        // Act
        var partiallyOnlineLinks = await repository
            .StreamLinksForExportAsync(OnlineState.PartiallyOnline, CancellationToken.None)
            .ToListAsync();
        var offlineLinks = await repository
            .StreamLinksForExportAsync(OnlineState.Offline, CancellationToken.None)
            .ToListAsync();
        var allLinks = await repository
            .StreamLinksForExportAsync(null, CancellationToken.None)
            .ToListAsync();

        // Assert
        partiallyOnlineLinks
            .Select(link => (link.Link, link.UploadOnlineState, link.LinkOnlineState))
            .ShouldBe([
                ("https://hoster/online", OnlineState.PartiallyOnline, OnlineState.Online),
                ("https://hoster/offline", OnlineState.PartiallyOnline, OnlineState.Offline),
            ]);
        offlineLinks.Select(link => link.Link).ShouldBe(["https://hoster/fully-offline"]);
        allLinks.Count.ShouldBe(3);
    }

    [TestCase(UploadState.Uploading)]
    [TestCase(UploadState.Failed)]
    public async Task StreamLinksForExportAsync_UnfinishedUploadsAndBlankLinks_AreMissing(
        UploadState unfinishedUploadState
    )
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Export.2026-GRP");
        var completedUpload = testData.AddUpload(
            testData.AddUploadConfig(release, "Completed"),
            MidnightUtc,
            MidnightUtc
        );
        testData.AddUploadedFile(completedUpload, "a.part1.rar", "https://hoster/kept");
        testData.AddUploadedFile(completedUpload, "a.part2.rar", "");
        testData.AddUploadedFile(completedUpload, "a.part3.rar", "   ");
        var unfinishedUpload = testData.AddUpload(
            testData.AddUploadConfig(release, "Unfinished"),
            MidnightUtc,
            MidnightUtc,
            unfinishedUploadState
        );
        testData.AddUploadedFile(unfinishedUpload, "b.part1.rar", "https://hoster/unfinished");
        await DbContext.SaveChangesAsync();

        // Act
        var links = await repository
            .StreamLinksForExportAsync(null, CancellationToken.None)
            .ToListAsync();

        // Assert
        links.Select(link => link.Link).ShouldBe(["https://hoster/kept"]);
    }

    [Test]
    public async Task StreamLinksForExportAsync_MultipleReleasesAndUploads_OrdersByReleaseUploadFileAndId()
    {
        // Arrange
        var laterRelease = testData.AddRelease("Bearcat.Second.2026-GRP");
        var laterReleaseUpload = testData.AddUpload(
            testData.AddUploadConfig(laterRelease, "Later"),
            MidnightUtc,
            MidnightUtc
        );
        testData.AddUploadedFile(laterReleaseUpload, "a.rar", "https://hoster/later-release");
        await DbContext.SaveChangesAsync();
        var earlierRelease = testData.AddRelease("Bearcat.First.2026-GRP");
        var firstUpload = testData.AddUpload(
            testData.AddUploadConfig(earlierRelease, "First"),
            MidnightUtc,
            MidnightUtc
        );
        await DbContext.SaveChangesAsync();
        var secondUpload = testData.AddUpload(
            testData.AddUploadConfig(earlierRelease, "Second"),
            MidnightUtc,
            MidnightUtc
        );
        await DbContext.SaveChangesAsync();
        testData.AddUploadedFile(secondUpload, "a.rar", "https://hoster/second-upload");
        testData.AddUploadedFile(firstUpload, "b.rar", "https://hoster/first-upload-b");
        testData.AddUploadedFile(firstUpload, "a.rar", "https://hoster/first-upload-a-1");
        await DbContext.SaveChangesAsync();
        testData.AddUploadedFile(firstUpload, "a.rar", "https://hoster/first-upload-a-2");
        await DbContext.SaveChangesAsync();

        // Act
        var links = await repository
            .StreamLinksForExportAsync(null, CancellationToken.None)
            .ToListAsync();

        // Assert
        links
            .Select(link => link.Link)
            .ShouldBe([
                "https://hoster/first-upload-a-1",
                "https://hoster/first-upload-a-2",
                "https://hoster/first-upload-b",
                "https://hoster/second-upload",
                "https://hoster/later-release",
            ]);
    }

    [Test]
    public async Task StreamLinksForExportAsync_PoolUploadAndVideoUpload_MapsHosterFileNameAndPassword()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Export.2026-GRP");
        var archiveConfig = testData.AddArchiveConfig(
            release,
            "Archive",
            archivePassword: "secret"
        );
        var poolConfig = testData.AddUploadConfig(release, "Pool", archiveConfig: archiveConfig);
        poolConfig.HosterRegistration = null;
        poolConfig.HosterAccountPool = new HosterAccountPool
        {
            Name = "Rapidgator pool",
            HosterClassName = "ExampleHoster",
        };
        var poolUpload = testData.AddUpload(poolConfig, MidnightUtc, MidnightUtc);
        var archiveLink = testData.AddUploadedFile(
            poolUpload,
            "/archives/a.part1.rar",
            "https://hoster/archive"
        );
        archiveLink.CheckedAt = MidnightUtc;
        var videoConfig = testData.AddVideoUploadConfig(release, "Video");
        var videoUpload = testData.AddUpload(videoConfig, MidnightUtc, MidnightUtc);
        videoUpload.HosterRegistration = null;
        testData.AddVideoUploadedFile(
            videoUpload,
            "videos/movie.mkv",
            "https://hoster/video",
            "https://hoster/embed"
        );
        await DbContext.SaveChangesAsync();

        // Act
        var links = await repository
            .StreamLinksForExportAsync(null, CancellationToken.None)
            .ToListAsync();

        // Assert
        var exportedArchiveLink = links.Single(link => link.Link == "https://hoster/archive");
        exportedArchiveLink.ReleaseName.ShouldBe("Bearcat.Export.2026-GRP");
        exportedArchiveLink.ReleaseGroupName.ShouldBe("Bearcat.Export.2026-GRP group");
        exportedArchiveLink.HosterName.ShouldBe("Rapidgator pool");
        exportedArchiveLink.UploadId.ShouldBe(poolUpload.Id);
        exportedArchiveLink.FileName.ShouldBe("a.part1.rar");
        exportedArchiveLink.CheckedAt.ShouldBe(MidnightUtc);
        exportedArchiveLink.ArchivePassword.ShouldBe("secret");

        var exportedVideoLink = links.Single(link => link.Link == "https://hoster/video");
        exportedVideoLink.HosterName.ShouldBe("Video hoster");
        exportedVideoLink.FileName.ShouldBe("movie.mkv");
        exportedVideoLink.EmbedLink.ShouldBe("https://hoster/embed");
        exportedVideoLink.CheckedAt.ShouldBeNull();
        exportedVideoLink.ArchivePassword.ShouldBeNull();
    }
}
