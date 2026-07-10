using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.LinkCrypter;
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
        repository = new ReleaseReadRepository(
            dbContext,
            Mock.Of<IArchiverFactory>(),
            Mock.Of<ILinkCrypterFactory>()
        );
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
        release.ReleaseNfo = new ReleaseNfo { FileName = "bearcat.nfo", Content = "nfo content" };
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
            }
        );
        release.ReleaseNfo = new ReleaseNfo { FileName = "bearcat.nfo", Content = "nfo content" };
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

    [Test]
    public async Task GetPostQueueAsync_ExcludesPostedReleases_AndGroupsByArchiveConfig()
    {
        // Arrange
        var openRelease = await AddReleaseWithUploadedFileAsync(
            "Bearcat.PostQueue.Open.2026-GRP",
            "Bearcat.PostQueue.Open.2026-GRP.part01.rar",
            "https://hoster.example/files/open"
        );

        var postedRelease = await AddReleaseWithUploadedFileAsync(
            "Bearcat.PostQueue.Posted.2026-GRP",
            "Bearcat.PostQueue.Posted.2026-GRP.part01.rar",
            "https://hoster.example/files/posted"
        );
        postedRelease.Release.UploadsPostedAt = DateTime.UtcNow.AddDays(1);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);

        // Assert
        var item = result.ShouldHaveSingleItem();
        item.ReleaseId.ShouldBe(openRelease.Release.Id);

        var archiveGroup = item.ArchiveGroups.ShouldHaveSingleItem();
        archiveGroup.ArchiveConfigName.ShouldBe("Bearcat.PostQueue.Open.2026-GRP archive");

        var hoster = archiveGroup.Hosters.ShouldHaveSingleItem();
        hoster.HosterRegistrationName.ShouldBe("Bearcat.PostQueue.Open.2026-GRP hoster");
        hoster.LinkCount.ShouldBe(1);
    }

    [Test]
    public async Task GetPostQueueAsync_AllActiveConfigsLatestCompleted_ShowsRelease()
    {
        // Arrange
        var release = await AddPostQueueReleaseAsync(
            "Bearcat.Ready.2026-GRP",
            [
                new PostQueueConfigSpec([new PostQueueUploadSpec(UploadState.Completed, 10)]),
                new PostQueueConfigSpec([new PostQueueUploadSpec(UploadState.Completed, 5)]),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        result.ShouldHaveSingleItem().ReleaseId.ShouldBe(release.Id);
    }

    [Test]
    public async Task GetPostQueueAsync_ActiveConfigWithLatestPendingUpload_HidesRelease()
    {
        // Arrange
        await AddPostQueueReleaseAsync(
            "Bearcat.Pending.2026-GRP",
            [
                new PostQueueConfigSpec([new PostQueueUploadSpec(UploadState.Completed, 10)]),
                new PostQueueConfigSpec([
                    new PostQueueUploadSpec(
                        UploadState.Pending,
                        5,
                        HasUploadedAt: false,
                        FileCount: 0
                    ),
                ]),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(0);
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPostQueueAsync_ActiveConfigWithoutUploads_HidesRelease()
    {
        // Arrange
        await AddPostQueueReleaseAsync(
            "Bearcat.Missing.2026-GRP",
            [
                new PostQueueConfigSpec([new PostQueueUploadSpec(UploadState.Completed, 10)]),
                new PostQueueConfigSpec([]),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(0);
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPostQueueAsync_OnlyInProgressUpload_HidesRelease()
    {
        // Arrange
        await AddPostQueueReleaseAsync(
            "Bearcat.Uploading.2026-GRP",
            [
                new PostQueueConfigSpec([
                    new PostQueueUploadSpec(
                        UploadState.Uploading,
                        5,
                        HasUploadedAt: false,
                        FileCount: 0
                    ),
                ]),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(0);
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPostQueueAsync_LatestUploadCompletedAfterCanceledReupload_ShowsRelease()
    {
        // Arrange
        var release = await AddPostQueueReleaseAsync(
            "Bearcat.Reupload.2026-GRP",
            [
                new PostQueueConfigSpec([
                    new PostQueueUploadSpec(
                        UploadState.Canceled,
                        60,
                        HasUploadedAt: false,
                        FileCount: 0
                    ),
                    new PostQueueUploadSpec(UploadState.Completed, 5),
                ]),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        result.ShouldHaveSingleItem().ReleaseId.ShouldBe(release.Id);
    }

    [Test]
    public async Task GetPostQueueAsync_LatestUploadFailedAfterCompleted_HidesRelease()
    {
        // Arrange
        await AddPostQueueReleaseAsync(
            "Bearcat.FailedReupload.2026-GRP",
            [
                new PostQueueConfigSpec([
                    new PostQueueUploadSpec(UploadState.Completed, 60),
                    new PostQueueUploadSpec(
                        UploadState.Failed,
                        5,
                        HasUploadedAt: false,
                        FileCount: 0
                    ),
                ]),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(0);
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPostQueueAsync_InactiveHosterConfigWithoutUploads_IsIgnored_ShowsRelease()
    {
        // Arrange
        var release = await AddPostQueueReleaseAsync(
            "Bearcat.InactiveMissing.2026-GRP",
            [
                new PostQueueConfigSpec([new PostQueueUploadSpec(UploadState.Completed, 10)]),
                new PostQueueConfigSpec([], HosterActive: false),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        result.ShouldHaveSingleItem().ReleaseId.ShouldBe(release.Id);
    }

    [Test]
    public async Task GetPostQueueAsync_InactiveHosterConfigWithPendingUpload_IsIgnored_ShowsRelease()
    {
        // Arrange
        var release = await AddPostQueueReleaseAsync(
            "Bearcat.InactivePending.2026-GRP",
            [
                new PostQueueConfigSpec([new PostQueueUploadSpec(UploadState.Completed, 10)]),
                new PostQueueConfigSpec(
                    [
                        new PostQueueUploadSpec(
                            UploadState.Pending,
                            5,
                            HasUploadedAt: false,
                            FileCount: 0
                        ),
                    ],
                    HosterActive: false
                ),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        result.ShouldHaveSingleItem().ReleaseId.ShouldBe(release.Id);
    }

    [Test]
    public async Task GetPostQueueAsync_ConfiguredLinkCrypterWithoutContainer_HidesRelease()
    {
        // Arrange
        await AddPostQueueReleaseAsync(
            "Bearcat.MissingContainer.2026-GRP",
            [
                new PostQueueConfigSpec(
                    [new PostQueueUploadSpec(UploadState.Completed, 10)],
                    LinkCrypters: [new PostQueueLinkCrypterSpec(HasContainer: false)]
                ),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(0);
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPostQueueAsync_ConfiguredLinkCrypterWithContainer_ShowsRelease()
    {
        // Arrange
        var release = await AddPostQueueReleaseAsync(
            "Bearcat.WithContainer.2026-GRP",
            [
                new PostQueueConfigSpec(
                    [new PostQueueUploadSpec(UploadState.Completed, 10)],
                    LinkCrypters: [new PostQueueLinkCrypterSpec(HasContainer: true)]
                ),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        result.ShouldHaveSingleItem().ReleaseId.ShouldBe(release.Id);
    }

    [Test]
    public async Task GetPostQueueAsync_InactiveLinkCrypterWithoutContainer_IsIgnored_ShowsRelease()
    {
        // Arrange
        var release = await AddPostQueueReleaseAsync(
            "Bearcat.InactiveCrypter.2026-GRP",
            [
                new PostQueueConfigSpec(
                    [new PostQueueUploadSpec(UploadState.Completed, 10)],
                    LinkCrypters:
                    [
                        new PostQueueLinkCrypterSpec(
                            HasContainer: false,
                            RegistrationActive: false
                        ),
                    ]
                ),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        result.ShouldHaveSingleItem().ReleaseId.ShouldBe(release.Id);
    }

    [Test]
    public async Task GetPostQueueAsync_CollectionScopedLinkCrypterWithoutReleaseContainer_IsIgnored_ShowsRelease()
    {
        // Arrange
        var release = await AddPostQueueReleaseAsync(
            "Bearcat.CollectionScopedCrypter.2026-GRP",
            [
                new PostQueueConfigSpec(
                    [new PostQueueUploadSpec(UploadState.Completed, 10)],
                    LinkCrypters:
                    [
                        new PostQueueLinkCrypterSpec(
                            HasContainer: false,
                            Scope: LinkCrypterContainerScope.ReleaseCollection
                        ),
                    ]
                ),
            ]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        result.ShouldHaveSingleItem().ReleaseId.ShouldBe(release.Id);
    }

    [Test]
    public async Task GetPostQueueAsync_ImageUploadConfigWithoutUpload_HidesRelease()
    {
        // Arrange
        await AddPostQueueReleaseAsync(
            "Bearcat.MissingImage.2026-GRP",
            [new PostQueueConfigSpec([new PostQueueUploadSpec(UploadState.Completed, 10)])],
            imageConfigs: [new PostQueueImageConfigSpec(HasUpload: false)]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(0);
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetPostQueueAsync_ImageUploadConfigWithUpload_ShowsRelease()
    {
        // Arrange
        var release = await AddPostQueueReleaseAsync(
            "Bearcat.WithImage.2026-GRP",
            [new PostQueueConfigSpec([new PostQueueUploadSpec(UploadState.Completed, 10)])],
            imageConfigs: [new PostQueueImageConfigSpec(HasUpload: true)]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        result.ShouldHaveSingleItem().ReleaseId.ShouldBe(release.Id);
    }

    [Test]
    public async Task GetPostQueueAsync_InactiveImageHosterWithoutUpload_IsIgnored_ShowsRelease()
    {
        // Arrange
        var release = await AddPostQueueReleaseAsync(
            "Bearcat.InactiveImageHoster.2026-GRP",
            [new PostQueueConfigSpec([new PostQueueUploadSpec(UploadState.Completed, 10)])],
            imageConfigs: [new PostQueueImageConfigSpec(HasUpload: false, HosterActive: false)]
        );
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetPostQueueAsync(CancellationToken.None);
        var count = await repository.CountPostQueueAsync(CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        result.ShouldHaveSingleItem().ReleaseId.ShouldBe(release.Id);
    }

    private async Task<Release> AddPostQueueReleaseAsync(
        string name,
        IReadOnlyList<PostQueueConfigSpec> configs,
        DateTime? uploadsPostedAt = null,
        IReadOnlyList<PostQueueImageConfigSpec>? imageConfigs = null
    )
    {
        var release = await AddReleaseAsync(name);
        release.UploadsPostedAt = uploadsPostedAt;

        var configIndex = 0;
        foreach (var configSpec in configs)
        {
            configIndex++;
            var archiveConfig = new ArchiveConfig
            {
                ReleaseId = release.Id,
                Name = $"{name} archive {configIndex}",
                ArchiveFilesBasePath = "/tmp/archives",
                ArchiverName = "RarArchiver",
                ArchiveFileSizeMb = 100,
                Archives = [],
                UploadConfigs = [],
            };
            var hosterRegistration = new HosterRegistration
            {
                Name = $"{name} hoster {configIndex}",
                SerializedConfig = "{}",
                HosterClassName = "ExampleHoster",
                IsActive = configSpec.HosterActive,
                UploadConfigs = [],
            };
            var uploadConfig = new UploadConfig
            {
                ReleaseId = release.Id,
                ArchiveConfig = archiveConfig,
                HosterRegistration = hosterRegistration,
                Name = $"{name} upload {configIndex}",
                LinkCrypters = [],
                Uploads = [],
            };
            dbContext.AddRange(archiveConfig, hosterRegistration, uploadConfig);

            foreach (var uploadSpec in configSpec.Uploads)
            {
                AddUploadToConfig(name, uploadConfig, uploadSpec);
            }

            var linkCrypterIndex = 0;
            foreach (var linkCrypterSpec in configSpec.LinkCrypters)
            {
                linkCrypterIndex++;
                AddLinkCrypterToConfig(
                    $"{name} {configIndex}-{linkCrypterIndex}",
                    uploadConfig,
                    linkCrypterSpec
                );
            }
        }

        var imageConfigIndex = 0;
        foreach (var imageConfigSpec in imageConfigs ?? [])
        {
            imageConfigIndex++;
            AddImageUploadConfig(release, $"{name} image {imageConfigIndex}", imageConfigSpec);
        }

        await dbContext.SaveChangesAsync();

        return release;
    }

    private void AddLinkCrypterToConfig(
        string name,
        UploadConfig uploadConfig,
        PostQueueLinkCrypterSpec spec
    )
    {
        var linkCrypterRegistration = new LinkCrypterRegistration
        {
            Name = $"{name} crypter",
            LinkCrypterClassName = "FileCrypt",
            SerializedConfig = "{}",
            IsActive = spec.RegistrationActive,
        };
        var uploadConfigLinkCrypter = new UploadConfigLinkCrypter
        {
            UploadConfig = uploadConfig,
            LinkCrypterRegistration = linkCrypterRegistration,
            ContainerScope = spec.Scope,
            LinkCrypterContainers = [],
        };
        dbContext.AddRange(linkCrypterRegistration, uploadConfigLinkCrypter);

        if (spec.HasContainer)
        {
            dbContext.Add(
                new LinkCrypterContainer
                {
                    Scope = spec.Scope,
                    UploadConfigLinkCrypter = uploadConfigLinkCrypter,
                    LinkCrypterRegistration = linkCrypterRegistration,
                    ContainerUrl = $"https://filecrypt.example/{name}",
                    State = LinkCrypterContainerState.Created,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }
    }

    private void AddImageUploadConfig(Release release, string name, PostQueueImageConfigSpec spec)
    {
        var imageHosterRegistration = new ImageHosterRegistration
        {
            Name = $"{name} hoster",
            ImageHosterClassName = "PiXhost",
            SerializedConfig = "{}",
            IsActive = spec.HosterActive,
        };
        var imageUploadConfig = new ImageUploadConfig
        {
            Release = release,
            ImageHosterRegistration = imageHosterRegistration,
            Name = name,
            ImageUploads = [],
        };
        dbContext.AddRange(imageHosterRegistration, imageUploadConfig);

        if (spec.HasUpload)
        {
            dbContext.Add(
                new ImageUpload
                {
                    ImageUploadConfig = imageUploadConfig,
                    CreatedAt = DateTime.UtcNow,
                    UploadedAt = DateTime.UtcNow,
                    UploadState = UploadState.Completed,
                }
            );
        }
    }

    private void AddUploadToConfig(string name, UploadConfig uploadConfig, PostQueueUploadSpec spec)
    {
        var createdAt = DateTime.UtcNow.AddMinutes(-spec.CreatedMinutesAgo);
        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            CreatedAt = createdAt,
            UploadedAt = spec.HasUploadedAt ? createdAt : null,
            UploadState = spec.State,
            OnlineState = OnlineState.Online,
            UploadedFiles = [],
            LinkCrypterContainers = [],
            Notifications = [],
        };

        if (spec.FileCount > 0)
        {
            var archive = new Archive
            {
                ArchiveConfig = uploadConfig.ArchiveConfig,
                ArchiveFolderPath = "/tmp/archives",
                CreatedAt = createdAt,
                ArchiveState = ArchiveState.Created,
                ArchiveFileSizeMb = 100,
                ArchiveFiles = [],
                Uploads = [],
                Notifications = [],
            };
            upload.Archive = archive;
            dbContext.Add(archive);

            for (var i = 0; i < spec.FileCount; i++)
            {
                var archiveFile = new ArchiveFile
                {
                    Archive = archive,
                    FullFileName = $"{name}.{uploadConfig.Name}.part{i:00}-{Guid.NewGuid():N}.rar",
                    UploadedFiles = [],
                };
                upload.UploadedFiles.Add(
                    new UploadedFile
                    {
                        Upload = upload,
                        ArchiveFile = archiveFile,
                        HosterFileLink = $"https://hoster.example/{name}/{Guid.NewGuid():N}",
                        OnlineState = OnlineState.Online,
                        CreatedAt = createdAt,
                    }
                );
                dbContext.Add(archiveFile);
            }
        }

        dbContext.Add(upload);
    }

    private sealed record PostQueueUploadSpec(
        UploadState State,
        int CreatedMinutesAgo,
        bool HasUploadedAt = true,
        int FileCount = 1
    );

    private sealed record PostQueueConfigSpec(
        IReadOnlyList<PostQueueUploadSpec> Uploads,
        bool HosterActive = true,
        IReadOnlyList<PostQueueLinkCrypterSpec> LinkCrypters = null!
    )
    {
        public IReadOnlyList<PostQueueLinkCrypterSpec> LinkCrypters { get; } = LinkCrypters ?? [];
    }

    private sealed record PostQueueLinkCrypterSpec(
        bool HasContainer,
        LinkCrypterContainerScope Scope = LinkCrypterContainerScope.Release,
        bool RegistrationActive = true
    );

    private sealed record PostQueueImageConfigSpec(bool HasUpload, bool HosterActive = true);

    [Test]
    public async Task GetUnmanagedArchiveFolderPathsAsync_ReturnsDistinctCreatedArchiveFolders()
    {
        // Arrange
        var releaseGroup = new ReleaseGroup
        {
            Name = "Unmanaged paths group",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };
        var release = new Release
        {
            Name = "Bearcat.Unmanaged.Paths",
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Unmanaged,
            ReleaseFolderPath = null,
            ReleaseGroup = releaseGroup,
            UploadConfigs = [],
            ArchiveConfigs =
            [
                BuildUnmanagedArchiveConfig("/data/archives/x", ArchiveState.Created),
                BuildUnmanagedArchiveConfig("/data/archives/y", ArchiveState.Created),
                BuildUnmanagedArchiveConfig("/data/archives/x", ArchiveState.Created),
                BuildUnmanagedArchiveConfig("/data/archives/z", ArchiveState.Deleted),
            ],
        };
        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetUnmanagedArchiveFolderPathsAsync(
            release.Id,
            CancellationToken.None
        );

        // Assert
        result.OrderBy(path => path).ShouldBe(["/data/archives/x", "/data/archives/y"]);
    }

    private static ArchiveConfig BuildUnmanagedArchiveConfig(
        string archiveFolderPath,
        ArchiveState archiveState
    )
    {
        return new ArchiveConfig
        {
            Name = "RAR",
            ArchiveFilesBasePath = archiveFolderPath,
            ArchiverName = "RarArchiver",
            ArchiveNamePrefix = null,
            ArchivePassword = null,
            ArchiveFileSizeMb = 0,
            UploadConfigs = [],
            Archives =
            [
                new Archive
                {
                    ArchiveFolderPath = archiveFolderPath,
                    CreatedAt = DateTime.UtcNow,
                    ArchiveState = archiveState,
                    ArchiveFileSizeMb = 0,
                    ArchiveFiles = [],
                    Uploads = [],
                    ErrorMessages = [],
                    Notifications = [],
                },
            ],
        };
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
