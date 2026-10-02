using Bearcat.Abstractions.Archiver;
using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Abstractions.LinkCrypter;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class ReleaseReadRepositoryOverviewTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private static readonly DateTime MidnightUtc = ReleaseReadRepositoryTestData.MidnightUtc;

    private ReleaseReadRepositoryTestData testData = null!;
    private ReleaseReadRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        testData = new ReleaseReadRepositoryTestData(DbContext);

        var archiverFactory = new Mock<IArchiverFactory>();
        archiverFactory
            .Setup(factory => factory.GetArchivers())
            .Returns([
                new ArchiverDto("RAR", "RarArchiver", ".rar"),
                new ArchiverDto("7-Zip", "SevenZipArchiver", ".7z"),
            ]);

        repository = new ReleaseReadRepository(
            ReadDbContext,
            archiverFactory.Object,
            Mock.Of<ILinkCrypterFactory>()
        );
    }

    [Test]
    public async Task GetReleaseOverviewAsync_SeveralUploadsPerConfig_ReturnsLatestUploadPerConfig()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Overview.2026-GRP");
        var passwordArchiveConfig = testData.AddArchiveConfig(
            release,
            "Password archive",
            archivePassword: "archive-secret"
        );
        var firstConfig = testData.AddUploadConfig(
            release,
            "Config A",
            testData.AddHosterRegistration("Hoster A"),
            passwordArchiveConfig
        );
        var latestUploadByUploadedAt = testData.AddUpload(
            firstConfig,
            MidnightUtc,
            MidnightUtc.AddHours(3).AddMilliseconds(500)
        );
        latestUploadByUploadedAt.NotFullyOnlineSince = MidnightUtc.AddHours(4).AddMilliseconds(250);
        testData.AddUploadedFile(
            latestUploadByUploadedAt,
            "Bearcat.Overview.part01.rar",
            "https://hoster.example/a1"
        );
        testData.AddUploadedFile(
            latestUploadByUploadedAt,
            "Bearcat.Overview.part02.rar",
            "https://hoster.example/a2"
        );
        testData.AddUpload(
            firstConfig,
            MidnightUtc.AddHours(2),
            null,
            UploadState.Pending,
            OnlineState.Unknown
        );

        var secondConfig = testData.AddUploadConfig(release, "Config B");
        var firstTiedUpload = testData.AddUpload(
            secondConfig,
            MidnightUtc.AddHours(1),
            MidnightUtc.AddHours(1),
            onlineState: OnlineState.Offline
        );
        firstTiedUpload.ErrorMessages = ["First tied upload error"];
        var secondTiedUpload = testData.AddUpload(
            secondConfig,
            MidnightUtc.AddHours(1),
            MidnightUtc.AddHours(1),
            onlineState: OnlineState.PartiallyOnline
        );
        secondTiedUpload.ErrorMessages = ["Second tied upload error"];

        testData.AddUploadConfig(release, "Config C");

        var otherRelease = testData.AddRelease("Bearcat.Other.2026-GRP");
        testData.AddUpload(
            testData.AddUploadConfig(otherRelease, "Config A"),
            MidnightUtc.AddDays(1),
            MidnightUtc.AddDays(1)
        );
        await DbContext.SaveChangesAsync();
        var expectedTiedUpload =
            firstTiedUpload.Id > secondTiedUpload.Id ? firstTiedUpload : secondTiedUpload;

        // Act
        var result = await repository.GetReleaseOverviewAsync(release.Id, CancellationToken.None);

        // Assert
        result
            .Select(item => item.UploadConfigName)
            .ShouldBe(["Config A", "Config B", "Config C"]);

        var first = result[0];
        first.UploadConfigId.ShouldBe(firstConfig.Id);
        first.HosterRegistrationName.ShouldBe("Hoster A");
        first.UploadId.ShouldBe(latestUploadByUploadedAt.Id);
        first.CreatedAt.ShouldBe(MidnightUtc);
        first.UploadedAt.ShouldBe(MidnightUtc.AddHours(3).AddMilliseconds(500));
        first.UploadState.ShouldBe(UploadState.Completed);
        first.OnlineState.ShouldBe(OnlineState.Online);
        first.NotFullyOnlineSince.ShouldBe(MidnightUtc.AddHours(4).AddMilliseconds(250));
        first.LinkCount.ShouldBe(2);
        first.ErrorMessages.ShouldBeEmpty();
        first.ArchivePassword.ShouldBe("archive-secret");
        first.LinkCrypterLinks.ShouldBeEmpty();

        var second = result[1];
        second.UploadId.ShouldBe(expectedTiedUpload.Id);
        second.OnlineState.ShouldBe(expectedTiedUpload.OnlineState);
        second.ErrorMessages.ShouldBe(expectedTiedUpload.ErrorMessages);
        second.LinkCount.ShouldBe(0);
        second.ArchivePassword.ShouldBeNull();

        var third = result[2];
        third.UploadId.ShouldBeNull();
        third.CreatedAt.ShouldBeNull();
        third.UploadedAt.ShouldBeNull();
        third.UploadState.ShouldBeNull();
        third.OnlineState.ShouldBeNull();
        third.LinkCount.ShouldBe(0);
        third.ErrorMessages.ShouldBeEmpty();
        third.LinkCrypterLinks.ShouldBeEmpty();
    }

    [Test]
    public async Task GetReleaseOverviewAsync_LatestUploadHasReleaseAndCollectionContainers_ReturnsLinksOrderedByRegistrationName()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Containers.2026-GRP");
        var uploadConfig = testData.AddUploadConfig(release, "Config A");
        var olderUpload = testData.AddUpload(uploadConfig, MidnightUtc, MidnightUtc);
        var latestUpload = testData.AddUpload(
            uploadConfig,
            MidnightUtc.AddMilliseconds(1),
            MidnightUtc.AddMilliseconds(1)
        );
        var otherRelease = testData.AddRelease("Bearcat.Other.2026-GRP");
        var otherReleaseUpload = testData.AddUpload(
            testData.AddUploadConfig(otherRelease, "Config A"),
            MidnightUtc,
            MidnightUtc
        );

        var releaseCrypter = testData.AddLinkCrypterRegistration("Crypter B", "FileCrypt");
        var collectionCrypter = testData.AddLinkCrypterRegistration("Crypter A", "KeepLinks");
        testData.AddReleaseContainer(
            olderUpload,
            releaseCrypter,
            "https://crypter.example/old",
            MidnightUtc
        );
        var releaseContainer = testData.AddReleaseContainer(
            latestUpload,
            releaseCrypter,
            "https://crypter.example/release",
            MidnightUtc.AddMinutes(5).AddMilliseconds(125)
        );
        releaseContainer.State = LinkCrypterContainerState.CreationFailed;
        releaseContainer.Errors = ["Container went offline"];
        var collectionContainer = testData.AddCollectionContainer(
            [latestUpload, otherReleaseUpload],
            collectionCrypter,
            "https://crypter.example/collection",
            MidnightUtc.AddDays(1)
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetReleaseOverviewAsync(release.Id, CancellationToken.None);

        // Assert
        var item = result.ShouldHaveSingleItem();
        item.UploadId.ShouldBe(latestUpload.Id);
        item.LinkCrypterLinks.Select(link => link.LinkCrypterRegistrationName)
            .ShouldBe(["Crypter A", "Crypter B"]);

        var collectionLink = item.LinkCrypterLinks[0];
        collectionLink.LinkCrypterContainerId.ShouldBe(collectionContainer.Id);
        collectionLink.LinkCrypterClassName.ShouldBe("KeepLinks");
        collectionLink.ContainerUrl.ShouldBe("https://crypter.example/collection");
        collectionLink.Scope.ShouldBe(LinkCrypterContainerScope.ReleaseCollection);
        collectionLink.State.ShouldBe(LinkCrypterContainerState.Created);
        collectionLink.CreatedAt.ShouldBe(MidnightUtc.AddDays(1));
        collectionLink.Errors.ShouldBeEmpty();

        var releaseLink = item.LinkCrypterLinks[1];
        releaseLink.LinkCrypterContainerId.ShouldBe(releaseContainer.Id);
        releaseLink.LinkCrypterClassName.ShouldBe("FileCrypt");
        releaseLink.ContainerUrl.ShouldBe("https://crypter.example/release");
        releaseLink.Scope.ShouldBe(LinkCrypterContainerScope.Release);
        releaseLink.State.ShouldBe(LinkCrypterContainerState.CreationFailed);
        releaseLink.CreatedAt.ShouldBe(MidnightUtc.AddMinutes(5).AddMilliseconds(125));
        releaseLink.Errors.ShouldBe(["Container went offline"]);
    }

    [Test]
    public async Task GetReleaseOverviewImageUploadsAsync_ConfigsWithZeroOneAndSeveralUploads_ReturnsLatestUploadWithOrderedUrls()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Images.2026-GRP");
        var configWithoutUpload = testData.AddImageUploadConfig("Image A", release);

        var configWithOneUpload = testData.AddImageUploadConfig("Image B", release);
        var singleUpload = testData.AddImageUpload(
            configWithOneUpload,
            MidnightUtc,
            MidnightUtc.AddMilliseconds(750),
            [
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Thumbnail,
                    "https://img.example/b-thumb"
                ),
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Full,
                    "https://img.example/b-full"
                ),
            ]
        );

        var configWithNewerUpload = testData.AddImageUploadConfig("Image C", release);
        var failedOldUpload = testData.AddImageUpload(
            configWithNewerUpload,
            MidnightUtc.AddMinutes(30),
            null,
            [],
            UploadState.Failed
        );
        failedOldUpload.ErrorMessages = ["Old failure"];
        testData.AddImageUpload(
            configWithNewerUpload,
            MidnightUtc.AddHours(1),
            MidnightUtc.AddHours(1),
            [
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Full,
                    "https://img.example/c-old"
                ),
            ]
        );
        var newestUpload = testData.AddImageUpload(
            configWithNewerUpload,
            MidnightUtc.AddHours(2),
            MidnightUtc.AddHours(2).AddMilliseconds(250),
            [
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Medium,
                    "https://img.example/c-medium"
                ),
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Full,
                    "https://img.example/c-full"
                ),
            ]
        );

        var configWithFailedRetry = testData.AddImageUploadConfig("Image D", release);
        testData.AddImageUpload(
            configWithFailedRetry,
            MidnightUtc,
            MidnightUtc.AddHours(1),
            [
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Full,
                    "https://img.example/d-full"
                ),
            ]
        );
        var failedRetryUpload = testData.AddImageUpload(
            configWithFailedRetry,
            MidnightUtc.AddHours(1).AddMilliseconds(500),
            null,
            [],
            UploadState.Failed
        );
        failedRetryUpload.ErrorMessages = ["Image hoster rejected file"];

        var otherRelease = testData.AddRelease("Bearcat.Other.2026-GRP");
        testData.AddImageUpload(
            testData.AddImageUploadConfig("Image A", otherRelease),
            MidnightUtc,
            MidnightUtc,
            [
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Full,
                    "https://img.example/other"
                ),
            ]
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetReleaseOverviewImageUploadsAsync(
            release.Id,
            CancellationToken.None
        );

        // Assert
        result
            .Select(item => item.ImageUploadConfigName)
            .ShouldBe(["Image A", "Image B", "Image C", "Image D"]);

        var withoutUpload = result[0];
        withoutUpload.ImageUploadConfigId.ShouldBe(configWithoutUpload.Id);
        withoutUpload.ImageHosterRegistrationName.ShouldBe("Image A image hoster");
        withoutUpload.ImageUploadId.ShouldBeNull();
        withoutUpload.CreatedAt.ShouldBeNull();
        withoutUpload.UploadedAt.ShouldBeNull();
        withoutUpload.UploadState.ShouldBeNull();
        withoutUpload.ErrorMessages.ShouldBeEmpty();
        withoutUpload.ImageUrls.ShouldBeEmpty();

        var withOneUpload = result[1];
        withOneUpload.ImageUploadId.ShouldBe(singleUpload.Id);
        withOneUpload.CreatedAt.ShouldBe(MidnightUtc);
        withOneUpload.UploadedAt.ShouldBe(MidnightUtc.AddMilliseconds(750));
        withOneUpload.UploadState.ShouldBe(UploadState.Completed);
        withOneUpload
            .ImageUrls.Select(url => (url.ImageSize, url.Url))
            .ShouldBe([
                (ImageSize.Full, "https://img.example/b-full"),
                (ImageSize.Thumbnail, "https://img.example/b-thumb"),
            ]);

        var withNewerUpload = result[2];
        withNewerUpload.ImageUploadId.ShouldBe(newestUpload.Id);
        withNewerUpload.UploadedAt.ShouldBe(MidnightUtc.AddHours(2).AddMilliseconds(250));
        withNewerUpload.ErrorMessages.ShouldBeEmpty();
        withNewerUpload
            .ImageUrls.Select(url => (url.ImageSize, url.Url))
            .ShouldBe([
                (ImageSize.Full, "https://img.example/c-full"),
                (ImageSize.Medium, "https://img.example/c-medium"),
            ]);

        var withFailedRetry = result[3];
        withFailedRetry.ImageUploadId.ShouldBe(failedRetryUpload.Id);
        withFailedRetry.CreatedAt.ShouldBe(MidnightUtc.AddHours(1).AddMilliseconds(500));
        withFailedRetry.UploadedAt.ShouldBeNull();
        withFailedRetry.UploadState.ShouldBe(UploadState.Failed);
        withFailedRetry.ErrorMessages.ShouldBe(["Image hoster rejected file"]);
        withFailedRetry.ImageUrls.ShouldBeEmpty();
    }

    [Test]
    public async Task GetReleaseImageLinksAsync_SeveralUploadsPerConfig_ReturnsUrlsOfLatestUploadOnly()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.ImageLinks.2026-GRP");
        var coverConfig = testData.AddImageUploadConfig("Cover", release);
        testData.AddImageUpload(
            coverConfig,
            MidnightUtc,
            MidnightUtc.AddHours(1),
            [
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Full,
                    "https://img.example/cover-old"
                ),
            ]
        );
        testData.AddImageUpload(
            coverConfig,
            MidnightUtc.AddHours(1),
            MidnightUtc.AddHours(1).AddMilliseconds(1),
            [
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Thumbnail,
                    "https://img.example/cover-thumb"
                ),
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Full,
                    "https://img.example/cover-full"
                ),
            ]
        );

        testData.AddImageUploadConfig("Empty", release);

        var screensConfig = testData.AddImageUploadConfig("Screens", release);
        testData.AddImageUpload(
            screensConfig,
            MidnightUtc,
            MidnightUtc.AddHours(3),
            [
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Full,
                    "https://img.example/screens"
                ),
            ]
        );
        testData.AddImageUpload(
            screensConfig,
            MidnightUtc.AddHours(2),
            null,
            [],
            UploadState.Failed
        );

        var otherRelease = testData.AddRelease("Bearcat.Other.2026-GRP");
        testData.AddImageUpload(
            testData.AddImageUploadConfig("Cover", otherRelease),
            MidnightUtc.AddDays(1),
            MidnightUtc.AddDays(1),
            [
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Full,
                    "https://img.example/other"
                ),
            ]
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetReleaseImageLinksAsync(release.Id, CancellationToken.None);

        // Assert
        result.Select(item => item.ImageUploadConfigName).ShouldBe(["Cover", "Empty", "Screens"]);
        result[0]
            .Urls.Select(url => (url.Size, url.Url))
            .ShouldBe(
                [
                    (ImageSize.Full, "https://img.example/cover-full"),
                    (ImageSize.Thumbnail, "https://img.example/cover-thumb"),
                ],
                ignoreOrder: true
            );
        result[1].Urls.ShouldBeEmpty();
        result[2]
            .Urls.Select(url => (url.Size, url.Url))
            .ShouldBe([(ImageSize.Full, "https://img.example/screens")]);
    }

    [Test]
    public async Task GetReleaseImageLinksAsync_UrlsStoredInDifferentSizeOrder_ReturnsUrlsOrderedByImageSize()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.ImageOrder.2026-GRP");
        testData.AddImageUpload(
            testData.AddImageUploadConfig("Cover", release),
            MidnightUtc,
            MidnightUtc,
            [
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Medium,
                    "https://img.example/medium"
                ),
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Thumbnail,
                    "https://img.example/thumb"
                ),
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Full,
                    "https://img.example/full"
                ),
            ]
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetReleaseImageLinksAsync(release.Id, CancellationToken.None);

        // Assert
        result
            .ShouldHaveSingleItem()
            .Urls.Select(url => url.Size)
            .ShouldBe([ImageSize.Full, ImageSize.Thumbnail, ImageSize.Medium]);
    }

    [Test]
    public async Task GetCollectionImageLinksAsync_UploadsWithSameTimestamp_ReturnsUrlsOfUploadWithHighestId()
    {
        // Arrange
        var releaseCollection = testData.AddReleaseCollection("Bearcat.S01.2026-GRP");
        var collectionConfig = testData.AddImageUploadConfig(
            "Banner",
            releaseCollection: releaseCollection
        );
        var firstUpload = testData.AddImageUpload(
            collectionConfig,
            MidnightUtc,
            MidnightUtc,
            [
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Full,
                    "https://img.example/first"
                ),
            ]
        );
        var secondUpload = testData.AddImageUpload(
            collectionConfig,
            MidnightUtc,
            MidnightUtc,
            [
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Full,
                    "https://img.example/second"
                ),
            ]
        );
        var release = testData.AddRelease("Bearcat.S01E01.2026-GRP");
        testData.AddImageUpload(
            testData.AddImageUploadConfig("Banner", release),
            MidnightUtc,
            MidnightUtc,
            [
                ReleaseReadRepositoryTestData.CreateImageUrl(
                    ImageSize.Full,
                    "https://img.example/release"
                ),
            ]
        );
        await DbContext.SaveChangesAsync();
        var expectedUrl =
            firstUpload.Id > secondUpload.Id
                ? "https://img.example/first"
                : "https://img.example/second";

        // Act
        var result = await repository.GetCollectionImageLinksAsync(
            releaseCollection.Id,
            CancellationToken.None
        );

        // Assert
        var link = result.ShouldHaveSingleItem();
        link.ImageUploadConfigName.ShouldBe("Banner");
        link.Urls.Select(url => (url.Size, url.Url)).ShouldBe([(ImageSize.Full, expectedUrl)]);
    }

    [Test]
    public async Task GetArchiveConfigsAsync_ConfigsWithArchivesAndAdditionalContents_ReturnsSummariesAndContents()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Archives.2026-GRP");
        var sevenZipConfig = testData.AddArchiveConfig(
            release,
            "Seven",
            "SevenZipArchiver",
            "archive-secret"
        );
        sevenZipConfig.ArchiveNamePrefix = "bearcat";
        sevenZipConfig.ArchiveFileSizeMb = 250;
        var olderArchive = testData.AddArchive(
            sevenZipConfig,
            MidnightUtc.AddMilliseconds(100),
            ArchiveState.Deleted
        );
        foreach (var part in new[] { "01", "02", "03" })
        {
            DbContext.ArchiveFiles.Add(
                new ArchiveFile
                {
                    Archive = olderArchive,
                    FullFileName = $"bearcat.part{part}.7z",
                    UploadedFiles = [],
                }
            );
        }

        var newerArchive = testData.AddArchive(
            sevenZipConfig,
            MidnightUtc.AddDays(1),
            ArchiveState.CreationFailed
        );
        newerArchive.ErrorMessages = ["Not enough disk space"];
        sevenZipConfig.AdditionalArchiveContents.Add(
            new AdditionalArchiveContent
            {
                Name = "Readme",
                Type = AdditionalArchiveContentType.TextFile,
                FileName = "readme.txt",
                TextContent = "Bearcat",
            }
        );
        sevenZipConfig.AdditionalArchiveContents.Add(
            new AdditionalArchiveContent
            {
                Name = "Logo",
                Type = AdditionalArchiveContentType.Path,
                SourcePath = "/data/logo.png",
            }
        );
        var rarConfig = testData.AddArchiveConfig(release, "Rar", "RarArchiver");

        var otherRelease = testData.AddRelease("Bearcat.Other.2026-GRP");
        testData.AddArchiveConfig(otherRelease, "Other", "RarArchiver");
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetArchiveConfigsAsync(release.Id, CancellationToken.None);

        // Assert
        result
            .Select(config => config.ArchiveConfigId)
            .ShouldBe([rarConfig.Id, sevenZipConfig.Id]);

        var rar = result[0];
        rar.Name.ShouldBe("Rar");
        rar.ArchiverName.ShouldBe("RarArchiver");
        rar.ArchiverDisplayName.ShouldBe("RAR");
        rar.ArchiveFileExtension.ShouldBe(".rar");
        rar.ArchivePassword.ShouldBeNull();
        rar.ArchiveSummaries.ShouldBeEmpty();
        rar.AdditionalArchiveContents.ShouldBeEmpty();

        var sevenZip = result[1];
        sevenZip.Name.ShouldBe("Seven");
        sevenZip.ArchiveFilesBasePath.ShouldBe("/data/archives");
        sevenZip.ArchiverDisplayName.ShouldBe("7-Zip");
        sevenZip.ArchiveFileExtension.ShouldBe(".7z");
        sevenZip.ArchiveNamePrefix.ShouldBe("bearcat");
        sevenZip.ArchivePassword.ShouldBe("archive-secret");
        sevenZip.ArchiveFileSizeMb.ShouldBe(250);
        sevenZip
            .ArchiveSummaries.Select(summary =>
                (
                    summary.ArchiveId,
                    summary.CreatedAt,
                    summary.ArchiveState,
                    summary.ArchiveFileCount
                )
            )
            .ShouldBe([
                (newerArchive.Id, MidnightUtc.AddDays(1), ArchiveState.CreationFailed, 0),
                (olderArchive.Id, MidnightUtc.AddMilliseconds(100), ArchiveState.Deleted, 3),
            ]);
        sevenZip.ArchiveSummaries[0].ErrorMessages.ShouldBe(["Not enough disk space"]);
        sevenZip.ArchiveSummaries[1].ErrorMessages.ShouldBeEmpty();
        sevenZip
            .AdditionalArchiveContents.Select(content => (content.Name, content.Type))
            .ShouldBe([
                ("Logo", AdditionalArchiveContentType.Path),
                ("Readme", AdditionalArchiveContentType.TextFile),
            ]);
    }

    [Test]
    public async Task GetMediaFilesAsync_FilesWithAndWithoutMediaInfo_ReturnsParsedMetadataOrderedByPath()
    {
        // Arrange
        var release = testData.AddRelease("Bearcat.Media.2026-GRP");
        release.MediaFiles.Add(
            new ReleaseMediaFile
            {
                RelativePath = "sample/bearcat.sample.mkv",
                SizeBytes = 1_048_576,
                MediaInfoJson = "{}",
                MediaInfoText = "No media info",
            }
        );
        release.MediaFiles.Add(
            new ReleaseMediaFile
            {
                RelativePath = "bearcat.mkv",
                SizeBytes = 5_368_709_120,
                MediaInfoJson = """
                {
                  "media": {
                    "track": [
                      { "@type": "General", "Format": "Matroska", "Duration": "5400.000" },
                      { "@type": "Video", "StreamOrder": "0", "Format": "AVC", "Width": "1920", "Height": "1080", "Default": "Yes" },
                      { "@type": "Audio", "StreamOrder": "1", "Format": "AC-3", "Language": "de", "Channels": "6" },
                      { "@type": "Text", "StreamOrder": "2", "Format": "UTF-8", "Language": "en", "Forced": "Yes" }
                    ]
                  }
                }
                """,
                MediaInfoText = "General\nFormat : Matroska",
            }
        );
        await DbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetMediaFilesAsync(release.Id, CancellationToken.None);

        // Assert
        result
            .Select(file => file.RelativePath)
            .ShouldBe(["bearcat.mkv", "sample/bearcat.sample.mkv"]);

        var mainFile = result[0];
        mainFile.SizeBytes.ShouldBe(5_368_709_120);
        mainFile.ContainerFormat.ShouldBe("Matroska");
        mainFile.Duration.ShouldBe(TimeSpan.FromMinutes(90));
        mainFile.MediaInfoText.ShouldBe("General\nFormat : Matroska");
        var videoStream = mainFile.VideoStream.ShouldNotBeNull();
        videoStream.Codec.ShouldBe("AVC");
        videoStream.Width.ShouldBe(1920);
        videoStream.Height.ShouldBe(1080);
        videoStream.IsDefault.ShouldBeTrue();
        var audioStream = mainFile.AudioStreams.ShouldHaveSingleItem();
        audioStream.StreamIndex.ShouldBe(1);
        audioStream.Codec.ShouldBe("AC-3");
        audioStream.Language.ShouldBe("de");
        audioStream.Channels.ShouldBe(6);
        var subtitleStream = mainFile.SubtitleStreams.ShouldHaveSingleItem();
        subtitleStream.Language.ShouldBe("en");
        subtitleStream.Forced.ShouldBeTrue();

        var sampleFile = result[1];
        sampleFile.SizeBytes.ShouldBe(1_048_576);
        sampleFile.ContainerFormat.ShouldBeNull();
        sampleFile.Duration.ShouldBeNull();
        sampleFile.VideoStream.ShouldBeNull();
        sampleFile.AudioStreams.ShouldBeEmpty();
        sampleFile.SubtitleStreams.ShouldBeEmpty();
        sampleFile.MediaInfoText.ShouldBe("No media info");
    }
}
