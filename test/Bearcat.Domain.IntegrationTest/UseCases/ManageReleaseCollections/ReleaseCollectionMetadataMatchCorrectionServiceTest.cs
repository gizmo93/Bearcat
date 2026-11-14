using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UploadSources;
using Bearcat.Domain.Shared.LinkCrypterContainers;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.MetadataMatchCorrection;
using Bearcat.Domain.UseCases.ManageReleases.MetadataMatchCorrection;
using Bearcat.Domain.UseCases.ManageReleases.Parsers;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseCollections;

public class ReleaseCollectionMetadataMatchCorrectionServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string MetadataDatabaseClassName = "TmdbMetadataDatabase";
    private const string SerializedConfig = "{\"apiKey\":\"secret\"}";

    private Mock<IMediaMetadataDatabase> metadataDatabaseMock = null!;
    private IMediaMetadataDatabaseConfig metadataDatabaseConfig = null!;
    private ReleaseCollectionMetadataMatchCorrectionService service = null!;

    [SetUp]
    public async Task Setup()
    {
        DbContext.MediaDatabaseRegistrations.Add(
            new MediaDatabaseRegistration
            {
                MediaDatabaseClassName = MetadataDatabaseClassName,
                SerializedConfig = SerializedConfig,
                IsActive = true,
            }
        );

        await DbContext.SaveChangesAsync();

        metadataDatabaseConfig = new Mock<IMediaMetadataDatabaseConfig>(MockBehavior.Strict).Object;
        metadataDatabaseMock = new Mock<IMediaMetadataDatabase>(MockBehavior.Strict);

        metadataDatabaseMock
            .SetupGet(database => database.SupportedMediaKinds)
            .Returns([MediaKind.TvSeries]);

        metadataDatabaseMock.SetupGet(database => database.ResolutionPriority).Returns(0);

        metadataDatabaseMock
            .Setup(database => database.DeserializeConfig(SerializedConfig))
            .Returns(metadataDatabaseConfig);

        var metadataDatabaseFactoryMock = new Mock<IMediaMetadataDatabaseFactory>(
            MockBehavior.Strict
        );

        metadataDatabaseFactoryMock
            .Setup(factory => factory.Get(MetadataDatabaseClassName))
            .Returns(metadataDatabaseMock.Object);

        var mediaMetadataResolver = new MediaMetadataResolver(
            new MediaMetadataResolverRepository(DbContext, NoOpSecretProtector.Instance),
            metadataDatabaseFactoryMock.Object,
            new Mock<ILogger<MediaMetadataResolver>>().Object
        );

        var infoRepository = new ReleaseCollectionInfoRepository(DbContext);
        var timeProvider = CreateTimeProvider();

        service = new ReleaseCollectionMetadataMatchCorrectionService(
            repository: infoRepository,
            mediaMetadataResolver: mediaMetadataResolver,
            releaseCollectionInfoResolutionService: new ReleaseCollectionInfoResolutionService(
                infoRepository,
                mediaMetadataResolver,
                new Mock<ILogger<ReleaseCollectionInfoResolutionService>>().Object,
                timeProvider
            ),
            releaseCollectionService: new ReleaseCollectionService(
                new ReleaseCollectionRepository(
                    DbContext,
                    DbContext,
                    Mock.Of<IMediaMetadataDatabaseFactory>()
                ),
                Mock.Of<ICollectionLinkCrypterContainerUpdater>(),
                TestUploadSourceValidator.Create(DbContext),
                timeProvider
            ),
            timeProvider: timeProvider
        );
    }

    [Test]
    public async Task LoadPreviewAsync_TmdbMovieUrl_ReturnsReferenceNotSupportedForContentType()
    {
        // Arrange
        var collection = await AddCollectionAsync(CreateRelease("Bodies.2023.S01E01-GRP"));

        // Act
        var result = await service.LoadPreviewAsync(
            collection.Id,
            "https://www.themoviedb.org/movie/12775",
            CancellationToken.None
        );

        // Assert
        result.Failure.ShouldBe(MetadataMatchCorrectionFailure.ReferenceNotSupportedForContentType);
        result.Preview.ShouldBeNull();
    }

    [Test]
    public async Task LoadPreviewAsync_CollectionWithoutReleases_ReturnsCollectionHasNoReleases()
    {
        // Arrange
        var collection = await AddCollectionAsync();

        // Act
        var result = await service.LoadPreviewAsync(
            collection.Id,
            "https://www.themoviedb.org/tv/1399",
            CancellationToken.None
        );

        // Assert
        result.Failure.ShouldBe(MetadataMatchCorrectionFailure.CollectionHasNoReleases);
        result.Preview.ShouldBeNull();
    }

    [Test]
    public async Task LoadPreviewAsync_TvdbUrl_ResolvesSeriesOnlyByTvdbId()
    {
        // Arrange
        var collection = await AddCollectionAsync(CreateRelease("Bodies.2023.S01E01-GRP"));
        var metadata = CreateGameOfThronesMetadata();

        metadataDatabaseMock
            .Setup(database =>
                database.GetByDatabaseIdAsync(
                    metadataDatabaseConfig,
                    It.Is<MediaMetadataLookup>(lookup =>
                        lookup.MediaKind == MediaKind.TvSeries
                        && lookup.TvdbIdOrSlug == "121361"
                        && lookup.TmdbId == null
                        && lookup.ImdbId == null
                        && lookup.Title == null
                        && lookup.LanguageCode == "de"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(metadata);

        // Act
        var result = await service.LoadPreviewAsync(
            collection.Id,
            "https://www.thetvdb.com/dereferrer/series/121361",
            CancellationToken.None
        );

        // Assert
        result.Failure.ShouldBeNull();
        result.Preview.ShouldNotBeNull();
        result.Preview.Metadata.ShouldBe(metadata);
    }

    [Test]
    public async Task ApplyAsync_TmdbReference_OverwritesMetadataAndStoresManualIdsOnAllReleases()
    {
        // Arrange
        var firstRelease = CreateRelease("Bodies.2023.S01E01-GRP");
        firstRelease.ExternalIdentifiers.AddRange([
            new ReleaseExternalIdentifier
            {
                Type = ExternalIdentifierType.Imdb,
                Value = "tt9999999",
                Source = ExternalIdentifierSource.Xrel,
            },
            new ReleaseExternalIdentifier
            {
                Type = ExternalIdentifierType.Imdb,
                Value = "tt1111111",
                Source = ExternalIdentifierSource.Manual,
            },
            new ReleaseExternalIdentifier
            {
                Type = ExternalIdentifierType.Steam,
                Value = "1172710",
                Source = ExternalIdentifierSource.Manual,
            },
        ]);

        var secondRelease = CreateRelease("Bodies.2023.S01E02-GRP");
        var collection = await AddCollectionAsync(firstRelease, secondRelease);

        collection.Metadata = new ReleaseCollectionMetadata
        {
            MetadataDatabaseClassName = ReleaseCollectionMetadata.ManualSource,
            Title = "Wrong series",
            Description = "Wrong description",
            CoverUrl = "https://images.test/wrong.jpg",
            MetadataDatabaseUrl = "https://metadata.test/wrong",
        };

        var imageUploadConfig = new ImageUploadConfig
        {
            ReleaseCollection = collection,
            ImageHosterRegistration = new ImageHosterRegistration
            {
                Name = "PiXhost",
                ImageHosterClassName = "PiXhost",
                SerializedConfig = "{}",
                IsActive = true,
            },
            Name = "Cover",
        };

        var completedUpload = new ImageUpload
        {
            ImageUploadConfig = imageUploadConfig,
            CreatedAt = DateTime.UtcNow,
            UploadedAt = DateTime.UtcNow,
            UploadState = UploadState.Completed,
            ImageUrls =
            [
                new ImageUploadUrl
                {
                    ImageSize = ImageSize.Full,
                    Url = "https://pixhost.example/wrong.jpg",
                },
            ],
        };

        DbContext.AddRange(imageUploadConfig, completedUpload);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var preview = new MetadataMatchCorrectionPreview(
            Reference: new MediaDatabaseReference(ExternalIdentifierType.Tmdb, "tv/1399"),
            DatabaseClassName: MetadataDatabaseClassName,
            Metadata: CreateGameOfThronesMetadata()
        );

        // Act
        await service.ApplyAsync(collection.Id, preview, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();

        var persistedCollection = await DbContext
            .ReleaseCollections.Include(item => item.Metadata)
            .SingleAsync();

        persistedCollection.MetadataCheckedAt.ShouldNotBeNull();
        persistedCollection.Metadata.ShouldNotBeNull();
        persistedCollection.Metadata.MetadataDatabaseClassName.ShouldBe(MetadataDatabaseClassName);
        persistedCollection.Metadata.Title.ShouldBe("Game of Thrones");
        persistedCollection.Metadata.Description.ShouldBeNull();
        persistedCollection.Metadata.CoverUrl.ShouldBe("https://images.test/got.jpg");
        persistedCollection.Metadata.MetadataDatabaseUrl.ShouldBe(
            "https://www.themoviedb.org/tv/1399"
        );

        var identifiersByRelease = await DbContext
            .Releases.Include(release => release.ExternalIdentifiers)
            .ToDictionaryAsync(
                release => release.Name,
                release =>
                    release
                        .ExternalIdentifiers.Select(identifier =>
                            (identifier.Type, identifier.Value, identifier.Source)
                        )
                        .ToList()
            );

        identifiersByRelease["Bodies.2023.S01E01-GRP"]
            .ShouldBe(
                [
                    (ExternalIdentifierType.Imdb, "tt9999999", ExternalIdentifierSource.Xrel),
                    (ExternalIdentifierType.Steam, "1172710", ExternalIdentifierSource.Manual),
                    (ExternalIdentifierType.Tmdb, "tv/1399", ExternalIdentifierSource.Manual),
                    (ExternalIdentifierType.Imdb, "tt0944947", ExternalIdentifierSource.Manual),
                ],
                ignoreOrder: true
            );

        identifiersByRelease["Bodies.2023.S01E02-GRP"]
            .ShouldBe(
                [
                    (ExternalIdentifierType.Tmdb, "tv/1399", ExternalIdentifierSource.Manual),
                    (ExternalIdentifierType.Imdb, "tt0944947", ExternalIdentifierSource.Manual),
                ],
                ignoreOrder: true
            );

        (await DbContext.ImageUploads.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task RemoveCorrectionAsync_ManualIdsOnReleases_RemovesThemAndResolvesMetadataAgain()
    {
        // Arrange
        var release = CreateRelease("Bodies.2023.S01E01-GRP");
        release.ExternalIdentifiers.AddRange([
            new ReleaseExternalIdentifier
            {
                Type = ExternalIdentifierType.Imdb,
                Value = "tt1234567",
                Source = ExternalIdentifierSource.Xrel,
            },
            new ReleaseExternalIdentifier
            {
                Type = ExternalIdentifierType.Imdb,
                Value = "tt0944947",
                Source = ExternalIdentifierSource.Manual,
            },
            new ReleaseExternalIdentifier
            {
                Type = ExternalIdentifierType.Tmdb,
                Value = "tv/1399",
                Source = ExternalIdentifierSource.Manual,
            },
        ]);

        var collection = await AddCollectionAsync(release);

        collection.Metadata = new ReleaseCollectionMetadata
        {
            MetadataDatabaseClassName = MetadataDatabaseClassName,
            Title = "Game of Thrones",
            CoverUrl = "https://images.test/got.jpg",
        };

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        metadataDatabaseMock
            .Setup(database =>
                database.GetByExternalIdAsync(
                    metadataDatabaseConfig,
                    It.Is<MediaMetadataLookup>(lookup =>
                        lookup.ImdbId == "tt1234567"
                        && lookup.TmdbId == null
                        && lookup.TvdbIdOrSlug == null
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new MediaMetadata(
                    Title: "Bodies",
                    Description: null,
                    Genre: null,
                    CoverUrl: "https://images.test/bodies.jpg",
                    DatabaseUrl: "https://www.themoviedb.org/tv/42",
                    ImdbId: "tt1234567",
                    TmdbId: "tv/42",
                    TvdbId: null
                )
            );

        // Act
        await service.RemoveCorrectionAsync(collection.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();

        var persistedCollection = await DbContext
            .ReleaseCollections.Include(item => item.Metadata)
            .SingleAsync();

        persistedCollection.MetadataCheckedAt.ShouldNotBeNull();
        persistedCollection.Metadata.ShouldNotBeNull();
        persistedCollection.Metadata.Title.ShouldBe("Bodies");
        persistedCollection.Metadata.CoverUrl.ShouldBe("https://images.test/bodies.jpg");

        var persistedIdentifiers = await DbContext.ReleaseExternalIdentifiers.ToListAsync();

        persistedIdentifiers
            .Select(identifier => (identifier.Type, identifier.Value, identifier.Source))
            .ShouldBe([(ExternalIdentifierType.Imdb, "tt1234567", ExternalIdentifierSource.Xrel)]);
    }

    private static MediaMetadata CreateGameOfThronesMetadata()
    {
        return new MediaMetadata(
            Title: "Game of Thrones",
            Description: null,
            Genre: null,
            CoverUrl: "https://images.test/got.jpg",
            DatabaseUrl: "https://www.themoviedb.org/tv/1399",
            ImdbId: "tt0944947",
            TmdbId: "tv/1399",
            TvdbId: "121361"
        );
    }

    private async Task<ReleaseCollection> AddCollectionAsync(params Release[] releases)
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };

        foreach (var release in releases)
        {
            release.ReleaseGroup = releaseGroup;
        }

        var collection = new ReleaseCollection
        {
            ReleaseGroup = releaseGroup,
            Key = $"key-{Guid.NewGuid():N}",
            Name = "Bodies.2023.S01.German.DL.1080p",
            PrimaryLanguageCode = "de",
            CreatedAt = DateTime.UtcNow,
            Releases = releases.ToList(),
        };

        DbContext.ReleaseCollections.Add(collection);
        await DbContext.SaveChangesAsync();

        return collection;
    }

    private static Release CreateRelease(string name)
    {
        return new Release
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseContentType = ReleaseContentType.TvShowEpisode,
            ArchiveConfigs = [],
            UploadConfigs = [],
        };
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
