using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using ReleaseInfo = Bearcat.Domain.Entities.ReleaseInfo;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class ReleaseReadRepositoryTest : BearcatIntegrationTest
{
    private BearcatDbContext dbContext = null!;
    private ReleaseReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        repository = new ReleaseReadRepository(dbContext, Mock.Of<IArchiverFactory>());
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task GetReleaseInfosAsync_ReleaseHasInfo_ReturnsTypedReadModel()
    {
        // Arrange
        var release = await AddReleaseAsync();
        dbContext.ReleaseInfos.Add(
            new ReleaseInfo
            {
                ReleaseId = release.Id,
                NfoDatabaseClassName = "XrelNfoDatabase",
                ReleaseName = "Bearcat.Release.2026-GRP",
                ReleaseDatabaseUrl = "https://www.xrel.to/release/123",
                SizeNumber = 12,
                SizeUnit = "GB",
                VideoType = "WEB",
                AudioType = "AC3",
                ExternalInfos =
                [
                    new ReleaseExternalInfo
                    {
                        Type = ExternalInfoType.Movie,
                        Title = "Bearcat Movie",
                        Urls =
                        [
                            new ReleaseExternalInfoUrl
                            {
                                Type = UrlType.Imdb,
                                Url = "https://www.imdb.com/de/title/tt1234567",
                            },
                            new ReleaseExternalInfoUrl
                            {
                                Type = UrlType.Other,
                                Url = "https://www.xrel.to/movie/123",
                            },
                        ],
                    },
                ],
            }
        );
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetReleaseInfosAsync(release.Id, CancellationToken.None);

        // Assert
        var releaseInfo = result.Single();
        releaseInfo.NfoDatabaseClassName.ShouldBe("XrelNfoDatabase");
        releaseInfo.ReleaseName.ShouldBe("Bearcat.Release.2026-GRP");
        releaseInfo.ReleaseDatabaseUrl.ShouldBe("https://www.xrel.to/release/123");
        releaseInfo.SizeNumber.ShouldBe(12);
        releaseInfo.SizeUnit.ShouldBe("GB");
        releaseInfo.VideoType.ShouldBe("WEB");
        releaseInfo.AudioType.ShouldBe("AC3");

        var externalInfo = releaseInfo.ExternalInfos.Single();
        externalInfo.Type.ShouldBe(ExternalInfoType.Movie);
        externalInfo.Title.ShouldBe("Bearcat Movie");
        externalInfo.Urls.ShouldContain(url =>
            url.Type == UrlType.Imdb && url.Url == "https://www.imdb.com/de/title/tt1234567"
        );
        externalInfo.Urls.ShouldContain(url =>
            url.Type == UrlType.Other && url.Url == "https://www.xrel.to/movie/123"
        );
    }

    [Test]
    public async Task GetReleaseInfosAsync_UrlsUseLegacyJsonShape_ReturnsTypedUrls()
    {
        // Arrange
        var release = await AddReleaseAsync();
        var releaseInfo = new ReleaseInfo
        {
            ReleaseId = release.Id,
            NfoDatabaseClassName = "XrelNfoDatabase",
            ReleaseName = "Bearcat.Release.2026-GRP",
            ExternalInfos = [],
        };
        dbContext.ReleaseInfos.Add(releaseInfo);
        await dbContext.SaveChangesAsync();

        const string legacyUrlsJson =
            """[{"type":1,"url":"https://www.imdb.com/de/title/tt1234567"}]""";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "ReleaseExternalInfos" ("ReleaseInfoId", "Title", "Type", "Urls")
             VALUES ({releaseInfo.Id}, {"Legacy Movie"}, {(int)ExternalInfoType.Movie}, {legacyUrlsJson}::jsonb)
             """
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetReleaseInfosAsync(release.Id, CancellationToken.None);

        // Assert
        var url = result.Single().ExternalInfos.Single().Urls.Single();

        url.Type.ShouldBe(UrlType.Imdb);
        url.Url.ShouldBe("https://www.imdb.com/de/title/tt1234567");
    }

    private async Task<Release> AddReleaseAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = "Release group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };
        var release = new Release
        {
            Name = "Bearcat.Release.2026-GRP",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = "/tmp/bearcat-release",
            ReleaseGroup = releaseGroup,
            ArchiveConfigs = [],
            UploadConfigs = [],
        };

        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();

        return release;
    }
}
