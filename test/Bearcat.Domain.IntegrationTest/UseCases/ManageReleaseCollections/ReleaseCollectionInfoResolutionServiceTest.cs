using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleases;
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
using ExternalInfoType = Bearcat.Abstractions.NfoDatabase.ExternalInfoType;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;
using UrlType = Bearcat.Abstractions.NfoDatabase.UrlType;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleaseCollections;

public class ReleaseCollectionInfoResolutionServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string MetadataDatabaseClassName = "TvdbMetadataDatabase";
    private const string SecondMetadataDatabaseClassName = "OtherMediaDatabase";
    private const string SerializedConfig = "{\"ApiKey\":\"secret\"}";

    private Mock<IMediaMetadataDatabaseFactory> metadataDatabaseFactoryMock = null!;
    private ReleaseCollectionInfoResolutionService service = null!;

    [SetUp]
    public void Setup()
    {
        metadataDatabaseFactoryMock = new Mock<IMediaMetadataDatabaseFactory>(MockBehavior.Strict);

        service = new ReleaseCollectionInfoResolutionService(
            new ReleaseCollectionInfoRepository(DbContext),
            new MediaMetadataResolver(
                new MediaMetadataResolverRepository(DbContext, NoOpSecretProtector.Instance),
                metadataDatabaseFactoryMock.Object,
                new Mock<ILogger<MediaMetadataResolver>>().Object
            ),
            new Mock<ILogger<ReleaseCollectionInfoResolutionService>>().Object,
            CreateTimeProvider()
        );
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_NfoContainsImdbId_ResolvesByImdbAndPersistsMetadata()
    {
        // Arrange
        await AddMediaDatabaseRegistrationAsync(MetadataDatabaseClassName, isActive: true);
        var collection = await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease(
                "Bodies.2023.S01E01.German.DL.1080p-GRP",
                nfoContent: "plot ... https://www.imdb.com/title/tt1234567/ ... end"
            )
        );
        collection.PrimaryLanguageCode = "de";
        await DbContext.SaveChangesAsync();

        var (database, config) = SetupMediaDatabase(MetadataDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetByExternalIdAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup =>
                        lookup.MediaKind == MediaKind.TvSeries
                        && lookup.ImdbId == "tt1234567"
                        && lookup.LanguageCode == "de"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateMetadata());

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(1);

        DbContext.ChangeTracker.Clear();
        var metadata = await DbContext.ReleaseCollectionMetadata.SingleAsync();

        metadata.ReleaseCollectionId.ShouldBe(collection.Id);
        metadata.MetadataDatabaseClassName.ShouldBe(MetadataDatabaseClassName);
        metadata.Title.ShouldBe("Bodies");
        metadata.Description.ShouldBe("Vier Detectives, ein Verbrechen.");
        metadata.CoverUrl.ShouldBe("https://artworks.thetvdb.com/banners/cover.jpg");
        metadata.MetadataDatabaseUrl.ShouldBe("https://www.thetvdb.com/series/bodies");

        var persistedCollection = await DbContext.ReleaseCollections.SingleAsync();
        persistedCollection.MetadataCheckedAt.ShouldNotBeNull();

        database.Verify(
            seriesDatabase =>
                seriesDatabase.GetByExternalIdAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup => lookup.ImdbId == "tt1234567"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        database.Verify(
            seriesDatabase =>
                seriesDatabase.GetByTitleAsync(
                    It.IsAny<IMediaMetadataDatabaseConfig>(),
                    It.IsAny<MediaMetadataLookup>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_NoNfoImdb_FallsBackToResolvedExternalInfoImdb()
    {
        // Arrange
        await AddMediaDatabaseRegistrationAsync(MetadataDatabaseClassName, isActive: true);
        await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease(
                "Bodies.2023.S01E01.German.DL.1080p-GRP",
                imdbExternalUrl: "https://www.imdb.com/title/tt7654321/"
            )
        );

        var (database, config) = SetupMediaDatabase(MetadataDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetByExternalIdAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup => lookup.ImdbId == "tt7654321"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateMetadata());

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(1);

        DbContext.ChangeTracker.Clear();
        var metadata = await DbContext.ReleaseCollectionMetadata.SingleAsync();
        metadata.Title.ShouldBe("Bodies");

        database.Verify(
            seriesDatabase =>
                seriesDatabase.GetByExternalIdAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup => lookup.ImdbId == "tt7654321"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_NoImdb_FallsBackToTitleExtractedFromCollectionName()
    {
        // Arrange
        await AddMediaDatabaseRegistrationAsync(MetadataDatabaseClassName, isActive: true);
        await AddCollectionAsync(
            "The.Bearcat.Files.S01.German.DL.1080p.WEB",
            CreateRelease("The.Bearcat.Files.S01E01.German.DL.1080p-GRP")
        );

        var (database, config) = SetupMediaDatabase(MetadataDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetByTitleAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup => lookup.Title == "The Bearcat Files"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateMetadata());

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(1);

        DbContext.ChangeTracker.Clear();
        var metadata = await DbContext.ReleaseCollectionMetadata.SingleAsync();
        metadata.MetadataDatabaseClassName.ShouldBe(MetadataDatabaseClassName);

        database.Verify(
            seriesDatabase =>
                seriesDatabase.GetByTitleAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup => lookup.Title == "The Bearcat Files"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_NoActiveRegistration_DoesNothing()
    {
        // Arrange
        await AddMediaDatabaseRegistrationAsync(MetadataDatabaseClassName, isActive: false);
        await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease("Bodies.2023.S01E01.German.DL.1080p-GRP")
        );

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(0);
        (await DbContext.ReleaseCollectionMetadata.AnyAsync()).ShouldBeFalse();
        metadataDatabaseFactoryMock.Verify(factory => factory.Get(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_NoMatchingSeries_MarksCheckedWithoutMetadata()
    {
        // Arrange
        await AddMediaDatabaseRegistrationAsync(MetadataDatabaseClassName, isActive: true);
        var collection = await AddCollectionAsync(
            "Unknown.Series.S01.German.DL.1080p",
            CreateRelease("Unknown.Series.S01E01.German.DL.1080p-GRP")
        );

        var (database, config) = SetupMediaDatabase(MetadataDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetByTitleAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup => lookup.Title == "Unknown Series"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((MediaMetadata?)null);

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(0);

        DbContext.ChangeTracker.Clear();
        (await DbContext.ReleaseCollectionMetadata.AnyAsync()).ShouldBeFalse();

        var persistedCollection = await DbContext.ReleaseCollections.SingleAsync(c =>
            c.Id == collection.Id
        );
        persistedCollection.MetadataCheckedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_FirstDatabaseReturnsNull_UsesNextDatabase()
    {
        // Arrange
        await AddMediaDatabaseRegistrationAsync(MetadataDatabaseClassName, isActive: true);
        await AddMediaDatabaseRegistrationAsync(SecondMetadataDatabaseClassName, isActive: true);
        await AddCollectionAsync(
            "Fallback.Series.S01.German.DL.1080p",
            CreateRelease(
                "Fallback.Series.S01E01.German.DL.1080p-GRP",
                nfoContent: "https://www.imdb.com/title/tt1111111/"
            )
        );

        var (firstDatabase, firstConfig) = SetupMediaDatabase(
            MetadataDatabaseClassName,
            priority: 0
        );
        firstDatabase
            .Setup(seriesDatabase =>
                seriesDatabase.GetByExternalIdAsync(
                    firstConfig,
                    It.Is<MediaMetadataLookup>(lookup => lookup.ImdbId == "tt1111111"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((MediaMetadata?)null);
        firstDatabase
            .Setup(seriesDatabase =>
                seriesDatabase.GetByTitleAsync(
                    firstConfig,
                    It.Is<MediaMetadataLookup>(lookup => lookup.Title == "Fallback Series"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((MediaMetadata?)null);

        var (secondDatabase, secondConfig) = SetupMediaDatabase(
            SecondMetadataDatabaseClassName,
            priority: 100
        );
        secondDatabase
            .Setup(seriesDatabase =>
                seriesDatabase.GetByExternalIdAsync(
                    secondConfig,
                    It.Is<MediaMetadataLookup>(lookup => lookup.ImdbId == "tt1111111"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateMetadata());

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(1);

        DbContext.ChangeTracker.Clear();
        var metadata = await DbContext.ReleaseCollectionMetadata.SingleAsync();
        metadata.MetadataDatabaseClassName.ShouldBe(SecondMetadataDatabaseClassName);

        firstDatabase.Verify(
            seriesDatabase =>
                seriesDatabase.GetByExternalIdAsync(
                    firstConfig,
                    It.Is<MediaMetadataLookup>(lookup => lookup.ImdbId == "tt1111111"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ResolveAsync_CollectionWithImdbNfo_PersistsMetadata()
    {
        // Arrange
        await AddMediaDatabaseRegistrationAsync(MetadataDatabaseClassName, isActive: true);
        var collection = await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease(
                "Bodies.2023.S01E01.German.DL.1080p-GRP",
                nfoContent: "https://www.imdb.com/title/tt1234567/"
            )
        );

        var (database, config) = SetupMediaDatabase(MetadataDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetByExternalIdAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup => lookup.ImdbId == "tt1234567"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateMetadata());

        // Act
        var resolved = await service.ResolveAsync(collection.Id, CancellationToken.None);

        // Assert
        resolved.ShouldBeTrue();

        DbContext.ChangeTracker.Clear();
        var metadata = await DbContext.ReleaseCollectionMetadata.SingleAsync();
        metadata.ReleaseCollectionId.ShouldBe(collection.Id);
        metadata.Title.ShouldBe("Bodies");
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_RateLimitExceeded_StopsAndMarksChecked()
    {
        // Arrange
        await AddMediaDatabaseRegistrationAsync(MetadataDatabaseClassName, isActive: true);
        var collection = await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease(
                "Bodies.2023.S01E01.German.DL.1080p-GRP",
                nfoContent: "https://www.imdb.com/title/tt1234567/"
            )
        );

        var (database, config) = SetupMediaDatabase(MetadataDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetByExternalIdAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup => lookup.ImdbId == "tt1234567"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new MediaMetadataDatabaseRateLimitExceededException(MetadataDatabaseClassName, null)
            );

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(0);

        DbContext.ChangeTracker.Clear();
        (await DbContext.ReleaseCollectionMetadata.AnyAsync()).ShouldBeFalse();
        var persistedCollection = await DbContext.ReleaseCollections.SingleAsync(c =>
            c.Id == collection.Id
        );
        persistedCollection.MetadataCheckedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_MediaDatabaseThrows_MarksCheckedWithoutMetadata()
    {
        // Arrange
        await AddMediaDatabaseRegistrationAsync(MetadataDatabaseClassName, isActive: true);
        var collection = await AddCollectionAsync(
            "Unknown.Series.S01.German.DL.1080p",
            CreateRelease("Unknown.Series.S01E01.German.DL.1080p-GRP")
        );

        var (database, config) = SetupMediaDatabase(MetadataDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetByTitleAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup => lookup.Title == "Unknown Series"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(0);

        DbContext.ChangeTracker.Clear();
        (await DbContext.ReleaseCollectionMetadata.AnyAsync()).ShouldBeFalse();
        var persistedCollection = await DbContext.ReleaseCollections.SingleAsync(c =>
            c.Id == collection.Id
        );
        persistedCollection.MetadataCheckedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task ResolveAsync_NoActiveRegistration_ReturnsFalse()
    {
        // Arrange
        await AddMediaDatabaseRegistrationAsync(MetadataDatabaseClassName, isActive: false);
        var collection = await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease("Bodies.2023.S01E01.German.DL.1080p-GRP")
        );

        // Act
        var resolved = await service.ResolveAsync(collection.Id, CancellationToken.None);

        // Assert
        resolved.ShouldBeFalse();
        metadataDatabaseFactoryMock.Verify(factory => factory.Get(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ResolveAsync_CollectionNotFound_ReturnsFalse()
    {
        // Arrange
        await AddMediaDatabaseRegistrationAsync(MetadataDatabaseClassName, isActive: true);
        SetupMediaDatabase(MetadataDatabaseClassName);

        // Act
        var resolved = await service.ResolveAsync(999, CancellationToken.None);

        // Assert
        resolved.ShouldBeFalse();
        (await DbContext.ReleaseCollectionMetadata.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ResolveAsync_CollectionAlreadyHasMetadata_RefreshesMetadata()
    {
        // Arrange
        await AddMediaDatabaseRegistrationAsync(MetadataDatabaseClassName, isActive: true);
        var collection = await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease("Bodies.2023.S01E01.German.DL.1080p-GRP")
        );
        collection.Metadata = new ReleaseCollectionMetadata
        {
            MetadataDatabaseClassName = MetadataDatabaseClassName,
            Title = "Bodies",
            Description = "Existing",
            CoverUrl = "https://artworks.example/cover.jpg",
            MetadataDatabaseUrl = "https://www.thetvdb.com/series/bodies",
        };
        DbContext.Update(collection);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var (database, config) = SetupMediaDatabase(MetadataDatabaseClassName);
        database
            .Setup(metadataDatabase =>
                metadataDatabase.GetByTitleAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup => lookup.Title == "Bodies"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateMetadata());

        // Act
        var resolved = await service.ResolveAsync(collection.Id, CancellationToken.None);

        // Assert
        resolved.ShouldBeTrue();
        DbContext.ChangeTracker.Clear();
        var metadata = await DbContext.ReleaseCollectionMetadata.SingleAsync();
        metadata.Description.ShouldBe("Vier Detectives, ein Verbrechen.");
        metadata.CoverUrl.ShouldBe("https://artworks.thetvdb.com/banners/cover.jpg");
    }

    [Test]
    public async Task ResolveAsync_ManualMetadata_ReturnsFalse()
    {
        var collection = await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease("Bodies.2023.S01E01.German.DL.1080p-GRP")
        );
        collection.Metadata = new ReleaseCollectionMetadata
        {
            MetadataDatabaseClassName = ReleaseCollectionMetadata.ManualSource,
            Title = "Bodies",
        };
        await DbContext.SaveChangesAsync();

        var resolved = await service.ResolveAsync(collection.Id, CancellationToken.None);

        resolved.ShouldBeFalse();
        metadataDatabaseFactoryMock.Verify(factory => factory.Get(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_OtherWorkerStoredMetadataFirst_KeepsExistingMetadataAndResolvesNextCollection()
    {
        // Arrange
        await AddMediaDatabaseRegistrationAsync(MetadataDatabaseClassName, isActive: true);
        var conflictingCollection = await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease("Bodies.2023.S01E01.German.DL.1080p-GRP")
        );
        var nextCollection = await AddCollectionAsync(
            "Hostage.2025.S01.German.DL.1080p",
            CreateRelease("Hostage.2025.S01E01.German.DL.1080p-GRP")
        );
        DbContext.ChangeTracker.Clear();

        var (database, config) = SetupMediaDatabase(MetadataDatabaseClassName);
        database
            .Setup(metadataDatabase =>
                metadataDatabase.GetByTitleAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup => lookup.Title == "Bodies"),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(async () =>
            {
                await AddMetadataFromOtherWorkerAsync(conflictingCollection.Id);
                return CreateMetadata();
            });
        database
            .Setup(metadataDatabase =>
                metadataDatabase.GetByTitleAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup => lookup.Title == "Hostage"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new MediaMetadata(
                    Title: "Hostage",
                    Description: "Geiselnahme",
                    Genre: null,
                    CoverUrl: "https://artworks.thetvdb.com/banners/hostage.jpg",
                    DatabaseUrl: "https://www.thetvdb.com/series/hostage"
                )
            );

        // Act
        await service.ProcessMissingCollectionMetadataAsync(CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        var metadata = await DbContext.ReleaseCollectionMetadata.ToListAsync();
        metadata.Count.ShouldBe(2);

        var conflictingMetadata = metadata.Single(item =>
            item.ReleaseCollectionId == conflictingCollection.Id
        );
        conflictingMetadata.MetadataDatabaseClassName.ShouldBe("OtherWorkerDatabase");
        conflictingMetadata.Title.ShouldBe("Bodies (other worker)");

        var nextMetadata = metadata.Single(item => item.ReleaseCollectionId == nextCollection.Id);
        nextMetadata.MetadataDatabaseClassName.ShouldBe(MetadataDatabaseClassName);
        nextMetadata.Title.ShouldBe("Hostage");
        nextMetadata.CoverUrl.ShouldBe("https://artworks.thetvdb.com/banners/hostage.jpg");
    }

    private async Task AddMetadataFromOtherWorkerAsync(int releaseCollectionId)
    {
        var otherWorkerDbContext = CreateDbContext();
        otherWorkerDbContext.ReleaseCollectionMetadata.Add(
            new ReleaseCollectionMetadata
            {
                ReleaseCollectionId = releaseCollectionId,
                MetadataDatabaseClassName = "OtherWorkerDatabase",
                Title = "Bodies (other worker)",
            }
        );
        await otherWorkerDbContext.SaveChangesAsync();
    }

    private (
        Mock<IMediaMetadataDatabase> Database,
        IMediaMetadataDatabaseConfig Config
    ) SetupMediaDatabase(string className, int priority = 0)
    {
        var configMock = new Mock<IMediaMetadataDatabaseConfig>(MockBehavior.Strict);
        var databaseMock = new Mock<IMediaMetadataDatabase>(MockBehavior.Strict);
        databaseMock.SetupGet(database => database.ResolutionPriority).Returns(priority);
        databaseMock
            .SetupGet(database => database.SupportedMediaKinds)
            .Returns([MediaKind.TvSeries]);
        databaseMock
            .Setup(database => database.DeserializeConfig(SerializedConfig))
            .Returns(configMock.Object);
        metadataDatabaseFactoryMock
            .Setup(factory => factory.Get(className))
            .Returns(databaseMock.Object);

        return (databaseMock, configMock.Object);
    }

    private async Task AddMediaDatabaseRegistrationAsync(string className, bool isActive)
    {
        var registration = new MediaDatabaseRegistration
        {
            MediaDatabaseClassName = className,
            SerializedConfig = SerializedConfig,
            IsActive = isActive,
        };

        DbContext.MediaDatabaseRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();
    }

    private async Task<ReleaseCollection> AddCollectionAsync(string name, params Release[] releases)
    {
        var releaseGroup = await AddReleaseGroupAsync();

        foreach (var release in releases)
        {
            release.ReleaseGroupId = releaseGroup.Id;
        }

        var collection = new ReleaseCollection
        {
            ReleaseGroupId = releaseGroup.Id,
            Key = $"key-{Guid.NewGuid():N}",
            Name = name,
            CreatedAt = DateTime.UtcNow,
            Releases = releases.ToList(),
        };

        DbContext.ReleaseCollections.Add(collection);
        await DbContext.SaveChangesAsync();

        return collection;
    }

    private async Task<ReleaseGroup> AddReleaseGroupAsync()
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };

        DbContext.ReleaseGroups.Add(releaseGroup);
        await DbContext.SaveChangesAsync();

        return releaseGroup;
    }

    private static Release CreateRelease(
        string name,
        string? nfoContent = null,
        string? imdbExternalUrl = null
    )
    {
        var release = new Release
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ArchiveConfigs = [],
            UploadConfigs = [],
        };

        if (nfoContent is null && imdbExternalUrl is null)
        {
            return release;
        }

        var releaseInfo = new ReleaseInfo
        {
            NfoDatabaseClassName = "srrDB",
            ReleaseName = name,
            ExternalInfos = [],
        };

        if (nfoContent is not null)
        {
            release.ReleaseNfo = new ReleaseNfo { FileName = "release.nfo", Content = nfoContent };
            release.ExternalIdentifiers.AddRange(
                ImdbIdParser
                    .ExtractAll(nfoContent)
                    .Select(imdbId => new ReleaseExternalIdentifier
                    {
                        Type = ExternalIdentifierType.Imdb,
                        Value = imdbId,
                        Source = ExternalIdentifierSource.Nfo,
                    })
            );
        }

        if (imdbExternalUrl is not null)
        {
            release.ExternalIdentifiers.AddRange(
                ImdbIdParser
                    .ExtractAll(imdbExternalUrl)
                    .Select(imdbId => new ReleaseExternalIdentifier
                    {
                        Type = ExternalIdentifierType.Imdb,
                        Value = imdbId,
                        Source = ExternalIdentifierSource.Srrdb,
                    })
            );
            releaseInfo.ExternalInfos.Add(
                new ReleaseExternalInfo
                {
                    Type = ExternalInfoType.Tv,
                    Title = name,
                    Urls =
                    [
                        new ReleaseExternalInfoUrl { Type = UrlType.Imdb, Url = imdbExternalUrl },
                    ],
                }
            );
        }

        release.ReleaseInfo = releaseInfo;

        return release;
    }

    private static MediaMetadata CreateMetadata()
    {
        return new MediaMetadata(
            Title: "Bodies",
            Description: "Vier Detectives, ein Verbrechen.",
            Genre: null,
            CoverUrl: "https://artworks.thetvdb.com/banners/cover.jpg",
            DatabaseUrl: "https://www.thetvdb.com/series/bodies"
        );
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
