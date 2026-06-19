using Bearcat.Abstractions.SeriesDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
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

public class ReleaseCollectionInfoResolutionServiceTest : BearcatIntegrationTest
{
    private const string SeriesDatabaseClassName = "TvdbSeriesDatabase";
    private const string SecondSeriesDatabaseClassName = "OtherSeriesDatabase";
    private const string SerializedConfig = "{\"ApiKey\":\"secret\"}";

    private BearcatDbContext dbContext = null!;
    private Mock<ISeriesDatabaseFactory> seriesDatabaseFactoryMock = null!;
    private ReleaseCollectionInfoResolutionService service = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        seriesDatabaseFactoryMock = new Mock<ISeriesDatabaseFactory>(MockBehavior.Strict);

        service = new ReleaseCollectionInfoResolutionService(
            new ReleaseCollectionInfoRepository(dbContext, dbContext, NoOpSecretProtector.Instance),
            seriesDatabaseFactoryMock.Object,
            new Mock<ILogger<ReleaseCollectionInfoResolutionService>>().Object,
            CreateTimeProvider()
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_NfoContainsImdbId_ResolvesByImdbAndPersistsMetadata()
    {
        // Arrange
        await AddSeriesDatabaseRegistrationAsync(SeriesDatabaseClassName, isActive: true);
        var collection = await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease(
                "Bodies.2023.S01E01.German.DL.1080p-GRP",
                nfoContent: "plot ... https://www.imdb.com/title/tt1234567/ ... end"
            )
        );

        var (database, config) = SetupSeriesDatabase(SeriesDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetSeriesInfoByImdbIdAsync(
                    config,
                    "tt1234567",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateSeriesInfo());

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(1);

        dbContext.ChangeTracker.Clear();
        var metadata = await dbContext.ReleaseCollectionMetadata.SingleAsync();

        metadata.ReleaseCollectionId.ShouldBe(collection.Id);
        metadata.SeriesDatabaseClassName.ShouldBe(SeriesDatabaseClassName);
        metadata.Title.ShouldBe("Bodies");
        metadata.Description.ShouldBe("Vier Detectives, ein Verbrechen.");
        metadata.CoverUrl.ShouldBe("https://artworks.thetvdb.com/banners/cover.jpg");
        metadata.SeriesDatabaseUrl.ShouldBe("https://www.thetvdb.com/series/bodies");

        var persistedCollection = await dbContext.ReleaseCollections.SingleAsync();
        persistedCollection.MetadataCheckedAt.ShouldNotBeNull();

        database.Verify(
            seriesDatabase =>
                seriesDatabase.GetSeriesInfoByImdbIdAsync(
                    config,
                    "tt1234567",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        database.Verify(
            seriesDatabase =>
                seriesDatabase.GetSeriesInfoByTitleAsync(
                    It.IsAny<ISeriesDatabaseConfig>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_NoNfoImdb_FallsBackToResolvedExternalInfoImdb()
    {
        // Arrange
        await AddSeriesDatabaseRegistrationAsync(SeriesDatabaseClassName, isActive: true);
        await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease(
                "Bodies.2023.S01E01.German.DL.1080p-GRP",
                imdbExternalUrl: "https://www.imdb.com/title/tt7654321/"
            )
        );

        var (database, config) = SetupSeriesDatabase(SeriesDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetSeriesInfoByImdbIdAsync(
                    config,
                    "tt7654321",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateSeriesInfo());

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(1);

        dbContext.ChangeTracker.Clear();
        var metadata = await dbContext.ReleaseCollectionMetadata.SingleAsync();
        metadata.Title.ShouldBe("Bodies");

        database.Verify(
            seriesDatabase =>
                seriesDatabase.GetSeriesInfoByImdbIdAsync(
                    config,
                    "tt7654321",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_NoImdb_FallsBackToTitleExtractedFromCollectionName()
    {
        // Arrange
        await AddSeriesDatabaseRegistrationAsync(SeriesDatabaseClassName, isActive: true);
        await AddCollectionAsync(
            "The.Bearcat.Files.S01.German.DL.1080p.WEB",
            CreateRelease("The.Bearcat.Files.S01E01.German.DL.1080p-GRP")
        );

        var (database, config) = SetupSeriesDatabase(SeriesDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetSeriesInfoByTitleAsync(
                    config,
                    "The Bearcat Files",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateSeriesInfo());

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(1);

        dbContext.ChangeTracker.Clear();
        var metadata = await dbContext.ReleaseCollectionMetadata.SingleAsync();
        metadata.SeriesDatabaseClassName.ShouldBe(SeriesDatabaseClassName);

        database.Verify(
            seriesDatabase =>
                seriesDatabase.GetSeriesInfoByTitleAsync(
                    config,
                    "The Bearcat Files",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_NoActiveRegistration_DoesNothing()
    {
        // Arrange
        await AddSeriesDatabaseRegistrationAsync(SeriesDatabaseClassName, isActive: false);
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
        (await dbContext.ReleaseCollectionMetadata.AnyAsync()).ShouldBeFalse();
        seriesDatabaseFactoryMock.Verify(factory => factory.Get(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_NoMatchingSeries_MarksCheckedWithoutMetadata()
    {
        // Arrange
        await AddSeriesDatabaseRegistrationAsync(SeriesDatabaseClassName, isActive: true);
        var collection = await AddCollectionAsync(
            "Unknown.Series.S01.German.DL.1080p",
            CreateRelease("Unknown.Series.S01E01.German.DL.1080p-GRP")
        );

        var (database, config) = SetupSeriesDatabase(SeriesDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetSeriesInfoByTitleAsync(
                    config,
                    "Unknown Series",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((SeriesInfo?)null);

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(0);

        dbContext.ChangeTracker.Clear();
        (await dbContext.ReleaseCollectionMetadata.AnyAsync()).ShouldBeFalse();

        var persistedCollection = await dbContext.ReleaseCollections.SingleAsync(c =>
            c.Id == collection.Id
        );
        persistedCollection.MetadataCheckedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_FirstDatabaseReturnsNull_UsesNextDatabase()
    {
        // Arrange
        await AddSeriesDatabaseRegistrationAsync(SeriesDatabaseClassName, isActive: true);
        await AddSeriesDatabaseRegistrationAsync(SecondSeriesDatabaseClassName, isActive: true);
        await AddCollectionAsync(
            "Fallback.Series.S01.German.DL.1080p",
            CreateRelease(
                "Fallback.Series.S01E01.German.DL.1080p-GRP",
                nfoContent: "https://www.imdb.com/title/tt1111111/"
            )
        );

        var (firstDatabase, firstConfig) = SetupSeriesDatabase(
            SeriesDatabaseClassName,
            priority: 0
        );
        firstDatabase
            .Setup(seriesDatabase =>
                seriesDatabase.GetSeriesInfoByImdbIdAsync(
                    firstConfig,
                    "tt1111111",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((SeriesInfo?)null);
        firstDatabase
            .Setup(seriesDatabase =>
                seriesDatabase.GetSeriesInfoByTitleAsync(
                    firstConfig,
                    "Fallback Series",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((SeriesInfo?)null);

        var (secondDatabase, secondConfig) = SetupSeriesDatabase(
            SecondSeriesDatabaseClassName,
            priority: 100
        );
        secondDatabase
            .Setup(seriesDatabase =>
                seriesDatabase.GetSeriesInfoByImdbIdAsync(
                    secondConfig,
                    "tt1111111",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateSeriesInfo());

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(1);

        dbContext.ChangeTracker.Clear();
        var metadata = await dbContext.ReleaseCollectionMetadata.SingleAsync();
        metadata.SeriesDatabaseClassName.ShouldBe(SecondSeriesDatabaseClassName);

        firstDatabase.Verify(
            seriesDatabase =>
                seriesDatabase.GetSeriesInfoByImdbIdAsync(
                    firstConfig,
                    "tt1111111",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ResolveAsync_CollectionWithImdbNfo_PersistsMetadata()
    {
        // Arrange
        await AddSeriesDatabaseRegistrationAsync(SeriesDatabaseClassName, isActive: true);
        var collection = await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease(
                "Bodies.2023.S01E01.German.DL.1080p-GRP",
                nfoContent: "https://www.imdb.com/title/tt1234567/"
            )
        );

        var (database, config) = SetupSeriesDatabase(SeriesDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetSeriesInfoByImdbIdAsync(
                    config,
                    "tt1234567",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateSeriesInfo());

        // Act
        var resolved = await service.ResolveAsync(collection.Id, CancellationToken.None);

        // Assert
        resolved.ShouldBeTrue();

        dbContext.ChangeTracker.Clear();
        var metadata = await dbContext.ReleaseCollectionMetadata.SingleAsync();
        metadata.ReleaseCollectionId.ShouldBe(collection.Id);
        metadata.Title.ShouldBe("Bodies");
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_RateLimitExceeded_StopsAndMarksChecked()
    {
        // Arrange
        await AddSeriesDatabaseRegistrationAsync(SeriesDatabaseClassName, isActive: true);
        var collection = await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease(
                "Bodies.2023.S01E01.German.DL.1080p-GRP",
                nfoContent: "https://www.imdb.com/title/tt1234567/"
            )
        );

        var (database, config) = SetupSeriesDatabase(SeriesDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetSeriesInfoByImdbIdAsync(
                    config,
                    "tt1234567",
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new SeriesDatabaseRateLimitExceededException(SeriesDatabaseClassName, null)
            );

        // Act
        var resolvedCount = await service.ProcessMissingCollectionMetadataAsync(
            CancellationToken.None
        );

        // Assert
        resolvedCount.ShouldBe(0);

        dbContext.ChangeTracker.Clear();
        (await dbContext.ReleaseCollectionMetadata.AnyAsync()).ShouldBeFalse();
        var persistedCollection = await dbContext.ReleaseCollections.SingleAsync(c =>
            c.Id == collection.Id
        );
        persistedCollection.MetadataCheckedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task ProcessMissingCollectionMetadataAsync_SeriesDatabaseThrows_MarksCheckedWithoutMetadata()
    {
        // Arrange
        await AddSeriesDatabaseRegistrationAsync(SeriesDatabaseClassName, isActive: true);
        var collection = await AddCollectionAsync(
            "Unknown.Series.S01.German.DL.1080p",
            CreateRelease("Unknown.Series.S01E01.German.DL.1080p-GRP")
        );

        var (database, config) = SetupSeriesDatabase(SeriesDatabaseClassName);
        database
            .Setup(seriesDatabase =>
                seriesDatabase.GetSeriesInfoByTitleAsync(
                    config,
                    "Unknown Series",
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

        dbContext.ChangeTracker.Clear();
        (await dbContext.ReleaseCollectionMetadata.AnyAsync()).ShouldBeFalse();
        var persistedCollection = await dbContext.ReleaseCollections.SingleAsync(c =>
            c.Id == collection.Id
        );
        persistedCollection.MetadataCheckedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task ResolveAsync_NoActiveRegistration_ReturnsFalse()
    {
        // Arrange
        await AddSeriesDatabaseRegistrationAsync(SeriesDatabaseClassName, isActive: false);
        var collection = await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease("Bodies.2023.S01E01.German.DL.1080p-GRP")
        );

        // Act
        var resolved = await service.ResolveAsync(collection.Id, CancellationToken.None);

        // Assert
        resolved.ShouldBeFalse();
        seriesDatabaseFactoryMock.Verify(factory => factory.Get(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ResolveAsync_CollectionNotFound_ReturnsFalse()
    {
        // Arrange
        await AddSeriesDatabaseRegistrationAsync(SeriesDatabaseClassName, isActive: true);
        SetupSeriesDatabase(SeriesDatabaseClassName);

        // Act
        var resolved = await service.ResolveAsync(999, CancellationToken.None);

        // Assert
        resolved.ShouldBeFalse();
        (await dbContext.ReleaseCollectionMetadata.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task ResolveAsync_CollectionAlreadyHasMetadata_ReturnsFalse()
    {
        // Arrange
        await AddSeriesDatabaseRegistrationAsync(SeriesDatabaseClassName, isActive: true);
        SetupSeriesDatabase(SeriesDatabaseClassName);
        var collection = await AddCollectionAsync(
            "Bodies.2023.S01.German.DL.1080p",
            CreateRelease("Bodies.2023.S01E01.German.DL.1080p-GRP")
        );
        collection.Metadata = new ReleaseCollectionMetadata
        {
            SeriesDatabaseClassName = SeriesDatabaseClassName,
            Title = "Bodies",
            Description = "Existing",
            CoverUrl = "https://artworks.example/cover.jpg",
            SeriesDatabaseUrl = "https://www.thetvdb.com/series/bodies",
        };
        dbContext.Update(collection);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Act
        var resolved = await service.ResolveAsync(collection.Id, CancellationToken.None);

        // Assert
        resolved.ShouldBeFalse();
    }

    private (Mock<ISeriesDatabase> Database, ISeriesDatabaseConfig Config) SetupSeriesDatabase(
        string className,
        int priority = 0
    )
    {
        var configMock = new Mock<ISeriesDatabaseConfig>(MockBehavior.Strict);
        var databaseMock = new Mock<ISeriesDatabase>(MockBehavior.Strict);
        databaseMock.SetupGet(database => database.ResolutionPriority).Returns(priority);
        databaseMock
            .Setup(database => database.DeserializeConfig(SerializedConfig))
            .Returns(configMock.Object);
        seriesDatabaseFactoryMock
            .Setup(factory => factory.Get(className))
            .Returns(databaseMock.Object);

        return (databaseMock, configMock.Object);
    }

    private async Task AddSeriesDatabaseRegistrationAsync(string className, bool isActive)
    {
        var registration = new SeriesDatabaseRegistration
        {
            SeriesDatabaseClassName = className,
            SerializedConfig = SerializedConfig,
            IsActive = isActive,
        };

        dbContext.SeriesDatabaseRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();
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

        dbContext.ReleaseCollections.Add(collection);
        await dbContext.SaveChangesAsync();

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

        dbContext.ReleaseGroups.Add(releaseGroup);
        await dbContext.SaveChangesAsync();

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
            releaseInfo.ReleaseNfo = new ReleaseNfo
            {
                FileName = "release.nfo",
                Content = nfoContent,
            };
        }

        if (imdbExternalUrl is not null)
        {
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

    private static SeriesInfo CreateSeriesInfo()
    {
        return new SeriesInfo(
            Title: "Bodies",
            Description: "Vier Detectives, ein Verbrechen.",
            CoverUrl: "https://artworks.thetvdb.com/banners/cover.jpg",
            SeriesDatabaseUrl: "https://www.thetvdb.com/series/bodies"
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
