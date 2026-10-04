using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Moq;
using Shouldly;

namespace Bearcat.Infrastructure.IntegrationTest.Database.Repositories.ReleaseCollections;

public class ReleaseCollectionRepositoryTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string TvdbMetadataDatabaseClassName = "TvdbMetadataDatabase";

    private static readonly DateTime LastSecondOfDay = new(
        2026,
        3,
        1,
        23,
        59,
        59,
        DateTimeKind.Utc
    );

    private ReleaseCollectionRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        var tvdbMetadataDatabase = new Mock<IMediaMetadataDatabase>();
        tvdbMetadataDatabase.SetupGet(database => database.Name).Returns("TheTVDB");
        var metadataDatabaseFactory = new Mock<IMediaMetadataDatabaseFactory>();
        metadataDatabaseFactory
            .Setup(factory => factory.GetByClassName())
            .Returns(
                new Dictionary<string, IMediaMetadataDatabase>
                {
                    [TvdbMetadataDatabaseClassName] = tvdbMetadataDatabase.Object,
                }
            );
        repository = new ReleaseCollectionRepository(
            DbContext,
            DbContext,
            metadataDatabaseFactory.Object
        );
    }

    [Test]
    public async Task SearchAsync_SearchTermWithNonAsciiCharacters_MatchesNameOrKeyCaseInsensitive()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Series");
        var createdAt = LastSecondOfDay.AddMilliseconds(250);
        var nameMatch = AddCollection(releaseGroup, "Éclair in Town", "eclair.in.town");
        nameMatch.CreatedAt = createdAt;
        var keyMatch = AddCollection(releaseGroup, "Trouble in Town", "ÉCLAIRS.S01");
        AddCollection(releaseGroup, "Bodies", "bodies.s01");
        AddRelease(releaseGroup, "Eclair.In.Town.S01E01", nameMatch);
        AddRelease(releaseGroup, "Eclair.In.Town.S01E02", nameMatch);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchAsync(
            new ReleaseCollectionSearchQuery(SearchTerm: " éCL "),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(2);
        result
            .Items.Select(item => item.ReleaseCollectionId)
            .ShouldBe([nameMatch.Id, keyMatch.Id], ignoreOrder: true);

        var nameMatchItem = result.Items.Single(item => item.ReleaseCollectionId == nameMatch.Id);
        nameMatchItem.Name.ShouldBe("Éclair in Town");
        nameMatchItem.Key.ShouldBe("eclair.in.town");
        nameMatchItem.ReleaseContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        nameMatchItem.ReleaseGroupId.ShouldBe(releaseGroup.Id);
        nameMatchItem.ReleaseGroupName.ShouldBe("Series");
        nameMatchItem.ReleaseCount.ShouldBe(2);
        nameMatchItem.CreatedAt.ShouldBe(createdAt);

        result
            .Items.Single(item => item.ReleaseCollectionId == keyMatch.Id)
            .ReleaseCount.ShouldBe(0);
    }

    [Test]
    public async Task SearchAsync_ContentTypeAndReleaseGroupFilter_ReturnsMatchingCollection()
    {
        // Arrange
        var seriesGroup = await AddReleaseGroupAsync("Series");
        var otherGroup = await AddReleaseGroupAsync("Other");
        var matchingCollection = AddCollection(seriesGroup, "Bodies S01", "bodies.s01");
        AddCollection(seriesGroup, "Bodies Movies", "bodies.movies", ReleaseContentType.Movie);
        AddCollection(otherGroup, "Bodies S02", "bodies.s02");
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchAsync(
            new ReleaseCollectionSearchQuery(
                ReleaseContentType: ReleaseContentType.TvShowEpisode,
                ReleaseGroupId: seriesGroup.Id
            ),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().ReleaseCollectionId.ShouldBe(matchingCollection.Id);
    }

    [Test]
    public async Task SearchAsync_SecondPage_ReturnsRemainingCollectionsOrderedByName()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Series");

        foreach (var number in new[] { 7, 3, 1, 6, 2, 5, 4 })
        {
            AddCollection(releaseGroup, $"Collection {number:00}", $"collection.{number:00}");
        }

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchAsync(
            new ReleaseCollectionSearchQuery(PageIndex: 1, PageSize: 5),
            CancellationToken.None
        );

        // Assert
        result.TotalCount.ShouldBe(7);
        result.PageIndex.ShouldBe(1);
        result.PageSize.ShouldBe(5);
        result.Items.Select(item => item.Name).ShouldBe(["Collection 06", "Collection 07"]);
    }

    [Test]
    public async Task GetDetailAsync_CollectionDoesNotExist_ReturnsNull()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Series");
        var collection = AddCollection(releaseGroup, "Hostage S01", "hostage.s01");
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetDetailAsync(collection.Id + 1, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetDetailAsync_ReleasesWithMultipleUploadsPerConfig_ReturnsLatestUploadPerReleaseAndConfig()
    {
        // Arrange
        var seed = await AddCollectionDetailSeedAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetDetailAsync(seed.CollectionId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.ReleaseCollectionId.ShouldBe(seed.CollectionId);
        result.Name.ShouldBe("Hostage S01");
        result.Key.ShouldBe("hostage.s01");
        result.ReleaseContentType.ShouldBe(ReleaseContentType.TvShowEpisode);
        result.ReleaseGroupId.ShouldBe(seed.ReleaseGroupId);
        result.ReleaseGroupName.ShouldBe("Series");
        result.PrimaryLanguageCode.ShouldBe("de");
        result.CreatedAt.ShouldBe(LastSecondOfDay.AddMilliseconds(10));
        result.UploadsPostedAt.ShouldBeNull();
        result.Metadata.ShouldBeNull();

        result.Releases.Select(release => release.ReleaseId).ShouldBe([seed.E01Id, seed.E02Id]);

        var e01 = result.Releases[0];
        e01.Name.ShouldBe("Hostage.S01E01");
        e01.ReleaseType.ShouldBe(ReleaseType.Managed);
        e01.CreatedAt.ShouldBe(LastSecondOfDay.AddDays(-1).AddMilliseconds(5));
        e01.ActiveUploadConfigsCount.ShouldBe(2);
        e01.OnlineUploadConfigsCount.ShouldBe(2);
        e01.NotFullyOnlineSince.ShouldBe(seed.E01EarliestNotFullyOnlineSince);
        e01.LatestUploads.ShouldBe(
            [
                new ReleaseLatestUploadReadModel(
                    UploadId: seed.E01RapidgatorLatestUploadId,
                    UploadConfigName: "E01 rapidgator",
                    CreatedAt: LastSecondOfDay.AddSeconds(1),
                    UploadedAt: null
                ),
                new ReleaseLatestUploadReadModel(
                    UploadId: seed.E01DdownloadLatestUploadId,
                    UploadConfigName: "E01 ddownload",
                    CreatedAt: LastSecondOfDay.AddMinutes(-2),
                    UploadedAt: LastSecondOfDay.AddMilliseconds(700)
                ),
            ],
            ignoreOrder: true
        );

        var e02 = result.Releases[1];
        e02.Name.ShouldBe("Hostage.S01E02");
        e02.ActiveUploadConfigsCount.ShouldBe(2);
        e02.OnlineUploadConfigsCount.ShouldBe(1);
        e02.NotFullyOnlineSince.ShouldBe(seed.E02NotFullyOnlineSince);
        e02.LatestUploads.ShouldBe([
            new ReleaseLatestUploadReadModel(
                UploadId: seed.E02RapidgatorLatestUploadId,
                UploadConfigName: "E02 rapidgator",
                CreatedAt: LastSecondOfDay.AddSeconds(-20),
                UploadedAt: LastSecondOfDay.AddMilliseconds(500)
            ),
        ]);
    }

    [Test]
    public async Task GetDetailAsync_SlotsWithSharedLinkCryptersAndContainers_ReturnsSlotAggregates()
    {
        // Arrange
        var seed = await AddCollectionDetailSeedAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetDetailAsync(seed.CollectionId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.UploadSlots.Select(slot => slot.Name).ShouldBe(["Ddownload", "Rapidgator"]);

        var ddownloadSlot = result.UploadSlots[0];
        ddownloadSlot.CollectionUploadSlotId.ShouldBe(seed.DdownloadSlotId);
        ddownloadSlot.Key.ShouldBe("ddownload");
        ddownloadSlot.IsRequired.ShouldBeFalse();
        ddownloadSlot.PasswordPolicy.ShouldBe(CollectionUploadSlotPasswordPolicy.Ignore);
        ddownloadSlot.ExpectedArchivePassword.ShouldBeNull();
        ddownloadSlot.UploadConfigCount.ShouldBe(2);
        ddownloadSlot.UploadCount.ShouldBe(2);
        ddownloadSlot.SharedLinkCrypters.ShouldBeEmpty();
        ddownloadSlot.Containers.ShouldBeEmpty();

        var rapidgatorSlot = result.UploadSlots[1];
        rapidgatorSlot.CollectionUploadSlotId.ShouldBe(seed.RapidgatorSlotId);
        rapidgatorSlot.Key.ShouldBe("rapidgator");
        rapidgatorSlot.IsRequired.ShouldBeTrue();
        rapidgatorSlot.PasswordPolicy.ShouldBe(
            CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue
        );
        rapidgatorSlot.ExpectedArchivePassword.ShouldBe("slot-secret");
        rapidgatorSlot.UploadConfigCount.ShouldBe(2);
        rapidgatorSlot.UploadCount.ShouldBe(4);
        rapidgatorSlot.SharedLinkCrypters.ShouldBe([
            new CollectionUploadSlotLinkCrypterReadModel(
                LinkCrypterRegistrationId: seed.FilecryptRegistrationId,
                LinkCrypterRegistrationName: "Filecrypt",
                IsActive: true,
                Password: "e01-secret",
                EnableCaptcha: false,
                EnableContainerDownload: true,
                EnableClickAndLoad: false,
                UploadConfigCount: 2
            ),
            new CollectionUploadSlotLinkCrypterReadModel(
                LinkCrypterRegistrationId: seed.KeeplinksRegistrationId,
                LinkCrypterRegistrationName: "Keeplinks",
                IsActive: false,
                Password: "keeplinks-secret",
                EnableCaptcha: true,
                EnableContainerDownload: true,
                EnableClickAndLoad: true,
                UploadConfigCount: 1
            ),
        ]);

        rapidgatorSlot.Containers.Count.ShouldBe(2);
        var filecryptContainer = rapidgatorSlot.Containers[0];
        filecryptContainer.LinkCrypterContainerId.ShouldBe(seed.FilecryptContainerId);
        filecryptContainer.LinkCrypterRegistrationName.ShouldBe("Filecrypt");
        filecryptContainer.ContainerUrl.ShouldBe("https://filecrypt.example/hostage");
        filecryptContainer.State.ShouldBe(LinkCrypterContainerState.CreationFailed);
        filecryptContainer.CreatedAt.ShouldBe(LastSecondOfDay.AddMilliseconds(125));
        filecryptContainer.SourceUploadCount.ShouldBe(2);
        filecryptContainer.Errors.ShouldBe(["Captcha failed", "Retry later"]);

        var keeplinksContainer = rapidgatorSlot.Containers[1];
        keeplinksContainer.LinkCrypterContainerId.ShouldBe(seed.KeeplinksContainerId);
        keeplinksContainer.LinkCrypterRegistrationName.ShouldBe("Keeplinks");
        keeplinksContainer.State.ShouldBe(LinkCrypterContainerState.Created);
        keeplinksContainer.SourceUploadCount.ShouldBe(0);
        keeplinksContainer.Errors.ShouldBeEmpty();
    }

    [Test]
    public async Task GetDetailAsync_UploadsPostedAtSet_ReturnsUploadsPostedAt()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Series");
        var collection = AddCollection(releaseGroup, "Hostage S01", "hostage.s01");
        collection.UploadsPostedAt = LastSecondOfDay.AddMilliseconds(750);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetDetailAsync(collection.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.UploadsPostedAt.ShouldBe(LastSecondOfDay.AddMilliseconds(750));
    }

    [Test]
    public async Task GetDetailAsync_CollectionHasMetadata_ReturnsMetadataWithDatabaseName()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Series");
        var collection = AddCollection(releaseGroup, "Hostage S01", "hostage.s01");
        collection.Metadata = new ReleaseCollectionMetadata
        {
            MetadataDatabaseClassName = TvdbMetadataDatabaseClassName,
            Title = "Hostage",
            Description = "Hostage drama",
            CoverUrl = "https://artworks.example/hostage.jpg",
            MetadataDatabaseUrl = "https://www.thetvdb.com/series/hostage",
        };
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetDetailAsync(collection.Id, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.UploadSlots.ShouldBeEmpty();
        result.Releases.ShouldBeEmpty();
        result.Metadata.ShouldBe(
            new ReleaseCollectionMetadataReadModel(
                MetadataDatabaseName: "TheTVDB",
                Title: "Hostage",
                Description: "Hostage drama",
                CoverUrl: "https://artworks.example/hostage.jpg",
                MetadataDatabaseUrl: "https://www.thetvdb.com/series/hostage"
            )
        );
    }

    [Test]
    public async Task GetArchiveConfigOptionsAsync_ConfigsOfDifferentReleases_ReturnsConfigsPresentInAllReleasesWithMaxSize()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Series");
        var collection = AddCollection(releaseGroup, "Hostage S01", "hostage.s01");
        var otherCollection = AddCollection(releaseGroup, "Other S01", "other.s01");
        var e01 = AddRelease(releaseGroup, "Hostage.S01E01", collection);
        var e02 = AddRelease(releaseGroup, "Hostage.S01E02", collection);
        var e03 = AddRelease(releaseGroup, "Hostage.S01E03", collection);
        var otherRelease = AddRelease(releaseGroup, "Other.S01E01", otherCollection);
        AddArchiveConfig(e01, "RAR 1GB", 1000);
        AddArchiveConfig(e02, "RAR 1GB", 1024);
        AddArchiveConfig(e03, "RAR 1GB", 900);
        AddArchiveConfig(e01, "Zip", 50);
        AddArchiveConfig(e02, "Zip", 75);
        AddArchiveConfig(e03, "Zip", 60);
        AddArchiveConfig(e01, "RAR 500MB", 500);
        AddArchiveConfig(e02, "RAR 500MB", 500);
        AddArchiveConfig(otherRelease, "RAR 500MB", 4096);
        AddArchiveConfig(e01, "7z", 100);
        AddArchiveConfig(e01, "7z", 200);
        AddArchiveConfig(e02, "7z", 300);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetArchiveConfigOptionsAsync(
            collection.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new CollectionArchiveConfigOptionReadModel("RAR 1GB", 3, 1024),
            new CollectionArchiveConfigOptionReadModel("Zip", 3, 75),
        ]);
    }

    [Test]
    public async Task GetArchiveConfigOptionsAsync_CollectionWithoutReleases_ReturnsEmptyList()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Series");
        var collection = AddCollection(releaseGroup, "Hostage S01", "hostage.s01");
        var releaseOutsideCollection = AddRelease(releaseGroup, "Hostage.S01E01", null);
        AddArchiveConfig(releaseOutsideCollection, "RAR 1GB", 1000);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetArchiveConfigOptionsAsync(
            collection.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task GetImageUploadsAsync_ConfigsWithAndWithoutUploads_ReturnsLatestUploadWithOrderedUrls()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Series");
        var collection = AddCollection(releaseGroup, "Hostage S01", "hostage.s01");
        var release = AddRelease(releaseGroup, "Hostage.S01E01", collection);
        var imageHosterRegistration = new ImageHosterRegistration
        {
            Name = "PiXhost",
            ImageHosterClassName = "PiXhost",
            SerializedConfig = "{}",
            IsActive = true,
        };
        var coverConfig = new ImageUploadConfig
        {
            ReleaseCollection = collection,
            ImageHosterRegistration = imageHosterRegistration,
            Name = "Cover",
        };
        var screensConfig = new ImageUploadConfig
        {
            ReleaseCollection = collection,
            ImageHosterRegistration = imageHosterRegistration,
            Name = "Screens",
        };
        var releaseConfig = new ImageUploadConfig
        {
            Release = release,
            ImageHosterRegistration = imageHosterRegistration,
            Name = "Release cover",
        };
        DbContext.AddRange(screensConfig, coverConfig, releaseConfig);
        await DbContext.SaveChangesAsync();

        var completedUpload = new ImageUpload
        {
            ImageUploadConfig = coverConfig,
            CreatedAt = LastSecondOfDay.AddMilliseconds(100),
            UploadedAt = LastSecondOfDay.AddMilliseconds(900),
            UploadState = UploadState.Completed,
            ErrorMessages = ["Thumbnail recompressed"],
            ImageUrls =
            [
                new ImageUploadUrl
                {
                    ImageSize = ImageSize.Thumbnail,
                    Url = "https://pixhost.example/thumb.jpg",
                },
                new ImageUploadUrl
                {
                    ImageSize = ImageSize.Medium,
                    Url = "https://pixhost.example/medium.jpg",
                },
                new ImageUploadUrl
                {
                    ImageSize = ImageSize.Full,
                    Url = "https://pixhost.example/full.jpg",
                },
            ],
        };
        DbContext.Add(completedUpload);
        await DbContext.SaveChangesAsync();
        DbContext.Add(
            new ImageUpload
            {
                ImageUploadConfig = coverConfig,
                CreatedAt = LastSecondOfDay.AddMilliseconds(500),
                UploadState = UploadState.Failed,
                ErrorMessages = ["Timeout"],
            }
        );
        DbContext.Add(
            new ImageUpload
            {
                ImageUploadConfig = releaseConfig,
                CreatedAt = LastSecondOfDay.AddSeconds(5),
                UploadedAt = LastSecondOfDay.AddSeconds(5),
                UploadState = UploadState.Completed,
            }
        );
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.GetImageUploadsAsync(collection.Id, CancellationToken.None);

        // Assert
        result.Select(config => config.Name).ShouldBe(["Cover", "Screens"]);

        var cover = result[0];
        cover.ImageUploadConfigId.ShouldBe(coverConfig.Id);
        cover.ImageHosterRegistrationId.ShouldBe(imageHosterRegistration.Id);
        cover.ImageHosterRegistrationName.ShouldBe("PiXhost");
        cover.ImageUploadId.ShouldBe(completedUpload.Id);
        cover.CreatedAt.ShouldBe(LastSecondOfDay.AddMilliseconds(100));
        cover.UploadedAt.ShouldBe(LastSecondOfDay.AddMilliseconds(900));
        cover.UploadState.ShouldBe(UploadState.Completed);
        cover.ErrorMessages.ShouldBe(["Thumbnail recompressed"]);
        cover.ImageUrls.ShouldBe([
            new CollectionImageUploadUrlReadModel(
                ImageSize.Full,
                "https://pixhost.example/full.jpg"
            ),
            new CollectionImageUploadUrlReadModel(
                ImageSize.Thumbnail,
                "https://pixhost.example/thumb.jpg"
            ),
            new CollectionImageUploadUrlReadModel(
                ImageSize.Medium,
                "https://pixhost.example/medium.jpg"
            ),
        ]);

        var screens = result[1];
        screens.ImageUploadConfigId.ShouldBe(screensConfig.Id);
        screens.ImageUploadId.ShouldBeNull();
        screens.CreatedAt.ShouldBeNull();
        screens.UploadedAt.ShouldBeNull();
        screens.UploadState.ShouldBeNull();
        screens.ErrorMessages.ShouldBeEmpty();
        screens.ImageUrls.ShouldBeEmpty();
    }

    [Test]
    public async Task SearchAvailableReleasesAsync_SearchTermWithNonAsciiCharacters_ReturnsMatchingReleasesOfGroupOutsideCollection()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Series");
        var otherGroup = await AddReleaseGroupAsync("Other");
        var collection = AddCollection(releaseGroup, "Éclair S01", "eclair.s01");
        var otherCollection = AddCollection(releaseGroup, "Éclair S02", "eclair.s02");
        AddRelease(releaseGroup, "Éclair.S01E01", collection);
        var releaseWithoutCollection = AddRelease(releaseGroup, "Éclair.S01E02", null);
        var releaseInOtherCollection = AddRelease(releaseGroup, "Éclair.S02E01", otherCollection);
        AddRelease(releaseGroup, "Bodies.S01E01", null);
        AddRelease(otherGroup, "Éclair.S01E03", null);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchAvailableReleasesAsync(
            collection.Id,
            " éCL ",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe([
            new AvailableReleaseReadModel(releaseWithoutCollection.Id, "Éclair.S01E02"),
            new AvailableReleaseReadModel(releaseInOtherCollection.Id, "Éclair.S02E01"),
        ]);
    }

    [Test]
    public async Task SearchAvailableReleasesAsync_NoSearchTerm_ReturnsFirstFiftyReleasesOrderedByName()
    {
        // Arrange
        var releaseGroup = await AddReleaseGroupAsync("Series");
        var collection = AddCollection(releaseGroup, "Hostage S01", "hostage.s01");

        foreach (var number in Enumerable.Range(0, 52).Reverse())
        {
            AddRelease(releaseGroup, $"Release.{number:00}", null);
        }

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.SearchAvailableReleasesAsync(
            collection.Id,
            null,
            CancellationToken.None
        );

        // Assert
        result
            .Select(release => release.Name)
            .ShouldBe(Enumerable.Range(0, 50).Select(number => $"Release.{number:00}").ToList());
    }

    private async Task<CollectionDetailSeed> AddCollectionDetailSeedAsync()
    {
        var releaseGroup = await AddReleaseGroupAsync("Series");
        var collection = AddCollection(releaseGroup, "Hostage S01", "hostage.s01");
        collection.PrimaryLanguageCode = "de";
        collection.CreatedAt = LastSecondOfDay.AddMilliseconds(10);
        var otherCollection = AddCollection(releaseGroup, "Other S01", "other.s01");
        var rapidgatorSlot = new CollectionUploadSlot
        {
            ReleaseCollection = collection,
            Key = "rapidgator",
            Name = "Rapidgator",
            IsRequired = true,
            PasswordPolicy = CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue,
            ExpectedArchivePassword = "slot-secret",
        };
        var ddownloadSlot = new CollectionUploadSlot
        {
            ReleaseCollection = collection,
            Key = "ddownload",
            Name = "Ddownload",
            PasswordPolicy = CollectionUploadSlotPasswordPolicy.Ignore,
        };
        var hosterRegistration = new HosterRegistration
        {
            Name = "Hoster",
            SerializedConfig = "{}",
            HosterClassName = "TestHoster",
            IsActive = true,
        };
        var filecrypt = new LinkCrypterRegistration
        {
            Name = "Filecrypt",
            LinkCrypterClassName = "FilecryptLinkCrypter",
            SerializedConfig = "{}",
            IsActive = true,
        };
        var keeplinks = new LinkCrypterRegistration
        {
            Name = "Keeplinks",
            LinkCrypterClassName = "KeeplinksLinkCrypter",
            SerializedConfig = "{}",
            IsActive = false,
        };
        DbContext.AddRange(rapidgatorSlot, ddownloadSlot, hosterRegistration, filecrypt, keeplinks);
        await DbContext.SaveChangesAsync();

        var e01 = AddRelease(releaseGroup, "Hostage.S01E01", collection);
        e01.CreatedAt = LastSecondOfDay.AddDays(-1).AddMilliseconds(5);
        var e01Rapidgator = AddUploadConfig(
            e01,
            "E01 rapidgator",
            rapidgatorSlot,
            hosterRegistration
        );
        var e01Ddownload = AddUploadConfig(e01, "E01 ddownload", ddownloadSlot, hosterRegistration);
        AddLinkCrypter(
            e01Rapidgator,
            filecrypt,
            LinkCrypterContainerScope.ReleaseCollection,
            "e01-secret",
            enableCaptcha: false,
            enableClickAndLoad: false
        );
        var e01ReleaseScopedLinkCrypter = AddLinkCrypter(
            e01Ddownload,
            filecrypt,
            LinkCrypterContainerScope.Release,
            "release-secret"
        );
        await DbContext.SaveChangesAsync();

        var e02 = AddRelease(releaseGroup, "Hostage.S01E02", collection);
        e02.CreatedAt = LastSecondOfDay.AddDays(-1).AddMilliseconds(6);
        var e02Rapidgator = AddUploadConfig(
            e02,
            "E02 rapidgator",
            rapidgatorSlot,
            hosterRegistration
        );
        AddUploadConfig(e02, "E02 ddownload", ddownloadSlot, hosterRegistration);
        AddLinkCrypter(
            e02Rapidgator,
            filecrypt,
            LinkCrypterContainerScope.ReleaseCollection,
            "e02-secret"
        );
        AddLinkCrypter(
            e02Rapidgator,
            keeplinks,
            LinkCrypterContainerScope.ReleaseCollection,
            "keeplinks-secret"
        );
        var otherRelease = AddRelease(releaseGroup, "Other.S01E01", otherCollection);
        var otherUploadConfig = AddUploadConfig(
            otherRelease,
            "Other rapidgator",
            null,
            hosterRegistration
        );
        await DbContext.SaveChangesAsync();

        var e01EarliestNotFullyOnlineSince = new DateTime(
            2026,
            2,
            28,
            8,
            0,
            0,
            250,
            DateTimeKind.Utc
        );
        var e02NotFullyOnlineSince = new DateTime(2026, 3, 1, 10, 15, 30, 125, DateTimeKind.Utc);

        await AddUploadAsync(
            e01Rapidgator,
            createdAt: LastSecondOfDay.AddMinutes(-1),
            uploadedAt: LastSecondOfDay.AddMilliseconds(900),
            OnlineState.Online
        );
        var e01RapidgatorLatestUpload = await AddUploadAsync(
            e01Rapidgator,
            createdAt: LastSecondOfDay.AddSeconds(1),
            uploadedAt: null,
            OnlineState.Offline,
            notFullyOnlineSince: LastSecondOfDay.AddSeconds(1)
        );
        var e01DdownloadLatestUpload = await AddUploadAsync(
            e01Ddownload,
            createdAt: LastSecondOfDay.AddMinutes(-2),
            uploadedAt: LastSecondOfDay.AddMilliseconds(700),
            OnlineState.Offline,
            notFullyOnlineSince: e01EarliestNotFullyOnlineSince
        );
        await AddUploadAsync(
            e01Ddownload,
            createdAt: LastSecondOfDay.AddMilliseconds(950),
            uploadedAt: LastSecondOfDay.AddMilliseconds(200),
            OnlineState.Online
        );
        await AddUploadAsync(
            e02Rapidgator,
            createdAt: LastSecondOfDay.AddSeconds(-30),
            uploadedAt: LastSecondOfDay.AddMilliseconds(500),
            OnlineState.Online
        );
        var e02RapidgatorLatestUpload = await AddUploadAsync(
            e02Rapidgator,
            createdAt: LastSecondOfDay.AddSeconds(-20),
            uploadedAt: LastSecondOfDay.AddMilliseconds(500),
            OnlineState.PartiallyOnline,
            notFullyOnlineSince: e02NotFullyOnlineSince
        );
        await AddUploadAsync(
            otherUploadConfig,
            createdAt: LastSecondOfDay.AddSeconds(10),
            uploadedAt: LastSecondOfDay.AddSeconds(10),
            OnlineState.Offline,
            notFullyOnlineSince: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        );

        var filecryptContainer = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.ReleaseCollection,
            CollectionUploadSlot = rapidgatorSlot,
            LinkCrypterRegistration = filecrypt,
            ContainerUrl = "https://filecrypt.example/hostage",
            State = LinkCrypterContainerState.CreationFailed,
            Errors = ["Captcha failed", "Retry later"],
            CreatedAt = LastSecondOfDay.AddMilliseconds(125),
            SourceUploads =
            [
                new LinkCrypterContainerSourceUpload { Upload = e01RapidgatorLatestUpload },
                new LinkCrypterContainerSourceUpload { Upload = e02RapidgatorLatestUpload },
            ],
        };
        var keeplinksContainer = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.ReleaseCollection,
            CollectionUploadSlot = rapidgatorSlot,
            LinkCrypterRegistration = keeplinks,
            ContainerUrl = "https://keeplinks.example/hostage",
            State = LinkCrypterContainerState.Created,
            CreatedAt = LastSecondOfDay,
        };
        var releaseScopedContainer = new LinkCrypterContainer
        {
            Scope = LinkCrypterContainerScope.Release,
            UploadConfigLinkCrypter = e01ReleaseScopedLinkCrypter,
            Upload = e01DdownloadLatestUpload,
            LinkCrypterRegistration = filecrypt,
            ContainerUrl = "https://filecrypt.example/e01",
            State = LinkCrypterContainerState.Created,
            CreatedAt = LastSecondOfDay,
        };
        DbContext.AddRange(keeplinksContainer, filecryptContainer, releaseScopedContainer);
        await DbContext.SaveChangesAsync();

        return new CollectionDetailSeed(
            CollectionId: collection.Id,
            ReleaseGroupId: releaseGroup.Id,
            E01Id: e01.Id,
            E02Id: e02.Id,
            RapidgatorSlotId: rapidgatorSlot.Id,
            DdownloadSlotId: ddownloadSlot.Id,
            FilecryptRegistrationId: filecrypt.Id,
            KeeplinksRegistrationId: keeplinks.Id,
            FilecryptContainerId: filecryptContainer.Id,
            KeeplinksContainerId: keeplinksContainer.Id,
            E01RapidgatorLatestUploadId: e01RapidgatorLatestUpload.Id,
            E01DdownloadLatestUploadId: e01DdownloadLatestUpload.Id,
            E02RapidgatorLatestUploadId: e02RapidgatorLatestUpload.Id,
            E01EarliestNotFullyOnlineSince: e01EarliestNotFullyOnlineSince,
            E02NotFullyOnlineSince: e02NotFullyOnlineSince
        );
    }

    private async Task<Upload> AddUploadAsync(
        UploadConfig uploadConfig,
        DateTime createdAt,
        DateTime? uploadedAt,
        OnlineState onlineState,
        DateTime? notFullyOnlineSince = null
    )
    {
        var upload = new Upload
        {
            UploadConfig = uploadConfig,
            CreatedAt = createdAt,
            UploadedAt = uploadedAt,
            UploadState = UploadState.Completed,
            OnlineState = onlineState,
            NotFullyOnlineSince = notFullyOnlineSince,
            UploadedFiles = [],
            LinkCrypterContainers = [],
            Notifications = [],
        };

        DbContext.Uploads.Add(upload);
        await DbContext.SaveChangesAsync();

        return upload;
    }

    private UploadConfig AddUploadConfig(
        Release release,
        string name,
        CollectionUploadSlot? collectionUploadSlot,
        HosterRegistration hosterRegistration
    )
    {
        var uploadConfig = new UploadConfig
        {
            Release = release,
            ArchiveConfig = AddArchiveConfig(release, $"{name} archive", 512),
            HosterRegistration = hosterRegistration,
            CollectionUploadSlot = collectionUploadSlot,
            Name = name,
            Uploads = [],
            LinkCrypters = [],
        };

        DbContext.UploadConfigs.Add(uploadConfig);

        return uploadConfig;
    }

    private UploadConfigLinkCrypter AddLinkCrypter(
        UploadConfig uploadConfig,
        LinkCrypterRegistration linkCrypterRegistration,
        LinkCrypterContainerScope containerScope,
        string password,
        bool enableCaptcha = true,
        bool enableClickAndLoad = true
    )
    {
        var linkCrypter = new UploadConfigLinkCrypter
        {
            UploadConfig = uploadConfig,
            LinkCrypterRegistration = linkCrypterRegistration,
            ContainerScope = containerScope,
            Password = password,
            EnableCaptcha = enableCaptcha,
            EnableContainerDownload = true,
            EnableClickAndLoad = enableClickAndLoad,
            LinkCrypterContainers = [],
        };

        DbContext.UploadConfigLinkCrypters.Add(linkCrypter);

        return linkCrypter;
    }

    private ArchiveConfig AddArchiveConfig(Release release, string name, int archiveFileSizeMb)
    {
        var archiveConfig = new ArchiveConfig
        {
            Release = release,
            Name = name,
            ArchiveFilesBasePath = "/tmp/archives",
            ArchiverName = "rar",
            ArchiveNamePrefix = release.Name,
            ArchiveFileSizeMb = archiveFileSizeMb,
            Archives = [],
            UploadConfigs = [],
        };

        DbContext.ArchiveConfigs.Add(archiveConfig);

        return archiveConfig;
    }

    private Release AddRelease(
        ReleaseGroup releaseGroup,
        string name,
        ReleaseCollection? releaseCollection
    )
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = LastSecondOfDay,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = releaseGroup,
            ReleaseCollection = releaseCollection,
        };

        DbContext.Releases.Add(release);

        return release;
    }

    private ReleaseCollection AddCollection(
        ReleaseGroup releaseGroup,
        string name,
        string key,
        ReleaseContentType releaseContentType = ReleaseContentType.TvShowEpisode
    )
    {
        var releaseCollection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Name = name,
            Key = key,
            ReleaseContentType = releaseContentType,
            CreatedAt = LastSecondOfDay,
        };

        DbContext.ReleaseCollections.Add(releaseCollection);

        return releaseCollection;
    }

    private async Task<ReleaseGroup> AddReleaseGroupAsync(string name)
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = name,
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
        };

        DbContext.ReleaseGroups.Add(releaseGroup);
        await DbContext.SaveChangesAsync();

        return releaseGroup;
    }

    private sealed record CollectionDetailSeed(
        int CollectionId,
        int ReleaseGroupId,
        int E01Id,
        int E02Id,
        int RapidgatorSlotId,
        int DdownloadSlotId,
        int FilecryptRegistrationId,
        int KeeplinksRegistrationId,
        int FilecryptContainerId,
        int KeeplinksContainerId,
        int E01RapidgatorLatestUploadId,
        int E01DdownloadLatestUploadId,
        int E02RapidgatorLatestUploadId,
        DateTime E01EarliestNotFullyOnlineSince,
        DateTime E02NotFullyOnlineSince
    );
}
