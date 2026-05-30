using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using ReleaseInfo = Bearcat.Domain.Entities.ReleaseInfo;
using ReleaseNfo = Bearcat.Domain.Entities.ReleaseNfo;

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
    public async Task SearchReleasesAsync_ReleaseTypeFilter_ReturnsMatchingReleases()
    {
        // Arrange
        await AddReleaseAsync("Bearcat.Managed.2026-GRP", ReleaseType.Managed);
        var unmanagedRelease = await AddReleaseAsync(
            "Bearcat.Unmanaged.2026-GRP",
            ReleaseType.Unmanaged
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(ReleaseType: ReleaseType.Unmanaged),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(unmanagedRelease.Id);
        result.Items.Single().ReleaseType.ShouldBe(ReleaseType.Unmanaged);
    }

    [Test]
    public async Task SearchReleasesAsync_DownloadLinkFilter_ReturnsMatchingRelease()
    {
        // Arrange
        await AddReleaseAsync("Bearcat.Other.2026-GRP");
        var matchingRelease = await AddReleaseWithUploadedFileAsync(
            "Bearcat.DownloadLink.2026-GRP",
            "Bearcat.DownloadLink.2026-GRP.part01.rar",
            "https://hoster.example/files/abc123"
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(DownloadLink: "files/abc123"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(matchingRelease.Release.Id);
    }

    [Test]
    public async Task SearchReleasesAsync_ArchiveFileNameFilter_ReturnsMatchingRelease()
    {
        // Arrange
        await AddReleaseAsync("Bearcat.Other.2026-GRP");
        var matchingRelease = await AddReleaseWithUploadedFileAsync(
            "Bearcat.ArchiveFile.2026-GRP",
            "Bearcat.ArchiveFile.2026-GRP.part02.rar",
            "https://hoster.example/files/archive"
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(ArchiveFileName: "part02.rar"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(matchingRelease.Release.Id);
    }

    [Test]
    public async Task SearchReleasesAsync_UploadIdFilter_ReturnsMatchingRelease()
    {
        // Arrange
        await AddReleaseAsync("Bearcat.Other.2026-GRP");
        var matchingRelease = await AddReleaseWithUploadedFileAsync(
            "Bearcat.UploadId.2026-GRP",
            "Bearcat.UploadId.2026-GRP.part01.rar",
            "https://hoster.example/files/upload"
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchReleasesAsync(
            new ReleaseSearchQuery(UploadId: $"#{matchingRelease.Upload.Id}"),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Single().ReleaseId.ShouldBe(matchingRelease.Release.Id);
    }

    [Test]
    public async Task GetReleaseInfoAsync_ReleaseHasInfo_ReturnsTypedReadModel()
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
                Genre = "Drama, Sci-Fi",
                Description = "Bearcat plot",
                CoverUrl = "https://uploads2.xrel.to/img_cover/movie123.JPG",
                ReleaseNfo = new ReleaseNfo { FileName = "bearcat.nfo", Content = "nfo content" },
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
        var result = await repository.GetReleaseInfoAsync(release.Id, CancellationToken.None);

        // Assert
        var releaseInfo = result.ShouldNotBeNull();
        releaseInfo.NfoDatabaseClassName.ShouldBe("XrelNfoDatabase");
        releaseInfo.ReleaseName.ShouldBe("Bearcat.Release.2026-GRP");
        releaseInfo.ReleaseDatabaseUrl.ShouldBe("https://www.xrel.to/release/123");
        releaseInfo.SizeNumber.ShouldBe(12);
        releaseInfo.SizeUnit.ShouldBe("GB");
        releaseInfo.VideoType.ShouldBe("WEB");
        releaseInfo.AudioType.ShouldBe("AC3");
        releaseInfo.Genre.ShouldBe("Drama, Sci-Fi");
        releaseInfo.Description.ShouldBe("Bearcat plot");
        releaseInfo.CoverUrl.ShouldBe("https://uploads2.xrel.to/img_cover/movie123.JPG");
        releaseInfo.ReleaseNfo.ShouldNotBeNull();
        releaseInfo.ReleaseNfo.FileName.ShouldBe("bearcat.nfo");
        releaseInfo.ReleaseNfo.Content.ShouldBe("nfo content");

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
    public async Task GetReleaseNfoAsync_ReleaseHasStoredNfo_ReturnsFirstStoredNfo()
    {
        // Arrange
        var release = await AddReleaseAsync();
        dbContext.ReleaseInfos.Add(
            new ReleaseInfo
            {
                ReleaseId = release.Id,
                NfoDatabaseClassName = "XrelNfoDatabase",
                ReleaseName = "Bearcat.Release.2026-GRP",
                ExternalInfos = [],
                ReleaseNfo = new ReleaseNfo { FileName = "bearcat.nfo", Content = "nfo content" },
            }
        );
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetReleaseNfoAsync(release.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.FileName.ShouldBe("bearcat.nfo");
        result.Content.ShouldBe("nfo content");
    }

    [Test]
    public async Task GetReleaseInfoAsync_UrlsUseLegacyJsonShape_ReturnsTypedUrls()
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
            VALUES ({releaseInfo.Id}, {"Legacy Movie"}, {(int)
                ExternalInfoType.Movie}, {legacyUrlsJson}::jsonb)
            """
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetReleaseInfoAsync(release.Id, CancellationToken.None);

        // Assert
        var url = result.ShouldNotBeNull().ExternalInfos.Single().Urls.Single();

        url.Type.ShouldBe(UrlType.Imdb);
        url.Url.ShouldBe("https://www.imdb.com/de/title/tt1234567");
    }

    private async Task<Release> AddReleaseAsync(
        string name = "Bearcat.Release.2026-GRP",
        ReleaseType releaseType = ReleaseType.Managed
    )
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"{name} group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };
        var release = new Release
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ReleaseType = releaseType,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = releaseGroup,
            ArchiveConfigs = [],
            UploadConfigs = [],
        };

        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();

        return release;
    }

    private async Task<(Release Release, Upload Upload)> AddReleaseWithUploadedFileAsync(
        string releaseName,
        string archiveFileName,
        string downloadLink
    )
    {
        var release = await AddReleaseAsync(releaseName);
        var archiveConfig = new ArchiveConfig
        {
            ReleaseId = release.Id,
            Name = $"{releaseName} archive",
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "RarArchiver",
            ArchiveFileSizeMb = 100,
            Archives = [],
            UploadConfigs = [],
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = $"{releaseName} hoster",
            SerializedConfig = "{}",
            HosterClassName = "ExampleHoster",
            IsActive = true,
            UploadConfigs = [],
        };
        var uploadConfig = new UploadConfig
        {
            ReleaseId = release.Id,
            ArchiveConfig = archiveConfig,
            HosterRegistration = hosterRegistration,
            Name = $"{releaseName} upload",
            LinksDistributedTo = [],
            LinkCrypters = [],
            Uploads = [],
        };
        var archive = new Archive
        {
            ArchiveConfig = archiveConfig,
            ArchiveFolderPath = "/tmp/archives",
            CreatedAt = DateTime.UtcNow,
            ArchiveState = ArchiveState.Created,
            ArchiveFileSizeMb = 100,
            ArchiveFiles = [],
            Uploads = [],
            Notifications = [],
        };
        var archiveFile = new ArchiveFile
        {
            Archive = archive,
            FullFileName = archiveFileName,
            UploadedFiles = [],
        };
        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            Archive = archive,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            OnlineState = OnlineState.Online,
            UploadedFiles = [],
            LinkCrypterContainers = [],
            Notifications = [],
        };
        var uploadedFile = new UploadedFile
        {
            Upload = upload,
            ArchiveFile = archiveFile,
            HosterFileLink = downloadLink,
            OnlineState = OnlineState.Online,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.AddRange(
            archiveConfig,
            hosterRegistration,
            uploadConfig,
            archive,
            archiveFile,
            upload,
            uploadedFile
        );
        await dbContext.SaveChangesAsync();

        return (release, upload);
    }
}
