using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.MetadataMatchCorrection;
using Bearcat.Domain.UseCases.ManageReleases.Parsers;
using Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;
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

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageReleases;

public class ReleaseMetadataMatchCorrectionServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string MetadataDatabaseClassName = "TmdbMetadataDatabase";
    private const string SerializedConfig = "{\"apiKey\":\"secret\"}";

    private Mock<IMediaMetadataDatabase> metadataDatabaseMock = null!;
    private IMediaMetadataDatabaseConfig metadataDatabaseConfig = null!;
    private ReleaseMetadataMatchCorrectionService service = null!;

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
            .Returns([MediaKind.Movie, MediaKind.TvEpisode]);

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

        var releaseInfoRepository = new ReleaseInfoRepository(
            DbContext,
            DbContext,
            NoOpSecretProtector.Instance
        );

        var mediaMetadataResolver = new MediaMetadataResolver(
            new MediaMetadataResolverRepository(DbContext, NoOpSecretProtector.Instance),
            metadataDatabaseFactoryMock.Object,
            new Mock<ILogger<MediaMetadataResolver>>().Object
        );

        var timeProvider = CreateTimeProvider();

        service = new ReleaseMetadataMatchCorrectionService(
            repository: releaseInfoRepository,
            mediaMetadataResolver: mediaMetadataResolver,
            releaseMetadataResolver: new ReleaseMetadataResolver(
                mediaMetadataResolver,
                new Mock<INfoDatabaseFactory>(MockBehavior.Strict).Object,
                new Mock<ILogger<ReleaseMetadataResolver>>().Object
            ),
            releaseInfoService: new ReleaseInfoService(
                releaseInfoRepository,
                new ReleaseClassificationService(
                    new ReleaseClassificationRepository(DbContext),
                    timeProvider,
                    new Mock<ILogger<ReleaseClassificationService>>().Object
                ),
                new Mock<ILogger<ReleaseInfoService>>().Object
            ),
            timeProvider: timeProvider
        );
    }

    [Test]
    public async Task LoadPreviewAsync_UnrecognizedInput_ReturnsInputNotRecognized()
    {
        // Arrange
        var release = await AddReleaseAsync(
            "Amok.1994.1080p.BluRay.x264-GRP",
            ReleaseContentType.Movie
        );

        // Act
        var result = await service.LoadPreviewAsync(
            release.Id,
            "not a reference",
            CancellationToken.None
        );

        // Assert
        result.Failure.ShouldBe(MetadataMatchCorrectionFailure.InputNotRecognized);
        result.Preview.ShouldBeNull();
    }

    [Test]
    public async Task LoadPreviewAsync_TmdbTvUrlForMovie_ReturnsReferenceNotSupportedForContentType()
    {
        // Arrange
        var release = await AddReleaseAsync(
            "Amok.1994.1080p.BluRay.x264-GRP",
            ReleaseContentType.Movie
        );

        // Act
        var result = await service.LoadPreviewAsync(
            release.Id,
            "https://www.themoviedb.org/tv/1399-game-of-thrones",
            CancellationToken.None
        );

        // Assert
        result.Failure.ShouldBe(MetadataMatchCorrectionFailure.ReferenceNotSupportedForContentType);
        result.Preview.ShouldBeNull();
    }

    [Test]
    public async Task LoadPreviewAsync_ImdbId_ResolvesWithoutTitleAndReturnsPreview()
    {
        // Arrange
        var release = await AddReleaseAsync(
            "Amok.1994.1080p.BluRay.x264-GRP",
            ReleaseContentType.Movie
        );

        var metadata = CreateFleshAndBloodMetadata();

        metadataDatabaseMock
            .Setup(database =>
                database.GetByExternalIdAsync(
                    metadataDatabaseConfig,
                    It.Is<MediaMetadataLookup>(lookup =>
                        lookup.MediaKind == MediaKind.Movie
                        && lookup.ImdbId == "tt0089153"
                        && lookup.TmdbId == null
                        && lookup.SteamAppId == null
                        && lookup.Title == null
                        && lookup.Year == null
                        && lookup.LanguageCode == "de"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(metadata);

        // Act
        var result = await service.LoadPreviewAsync(
            release.Id,
            "https://www.imdb.com/title/tt0089153/",
            CancellationToken.None
        );

        // Assert
        result.Failure.ShouldBeNull();
        result.Preview.ShouldNotBeNull();
        result.Preview.Reference.ShouldBe(
            new MediaDatabaseReference(ExternalIdentifierType.Imdb, "tt0089153")
        );

        result.Preview.DatabaseClassName.ShouldBe(MetadataDatabaseClassName);
        result.Preview.Metadata.ShouldBe(metadata);
    }

    [Test]
    public async Task ApplyAsync_ManualMetadataAndWrongIdentifiers_ReplacesMetadataAndManualIdentifiers()
    {
        // Arrange
        var release = await AddReleaseAsync(
            "Amok.1994.1080p.BluRay.x264-GRP",
            ReleaseContentType.Movie
        );

        release.Metadata = new ReleaseMetadata
        {
            MetadataDatabaseClassName = ReleaseMetadata.ManualSource,
            Title = "Wrong movie",
            Genre = "Drama",
            Description = "Wrong description",
            CoverUrl = "https://images.test/wrong.jpg",
            MetadataDatabaseUrl = "https://metadata.test/wrong",
        };

        release.ExternalIdentifiers.AddRange([
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
        ]);

        var imageUploadConfig = new ImageUploadConfig
        {
            Release = release,
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

        var failedUpload = new ImageUpload
        {
            ImageUploadConfig = imageUploadConfig,
            CreatedAt = DateTime.UtcNow,
            UploadState = UploadState.Failed,
            ErrorMessages = ["Timeout"],
        };

        DbContext.AddRange(imageUploadConfig, completedUpload, failedUpload);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var preview = new MetadataMatchCorrectionPreview(
            Reference: new MediaDatabaseReference(ExternalIdentifierType.Tmdb, "movie/12775"),
            DatabaseClassName: MetadataDatabaseClassName,
            Metadata: CreateFleshAndBloodMetadata()
        );

        // Act
        await service.ApplyAsync(release.Id, preview, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();

        var persistedRelease = await DbContext
            .Releases.Include(item => item.Metadata)
            .Include(item => item.ExternalIdentifiers)
            .SingleAsync();

        persistedRelease.MetadataCheckedAt.ShouldNotBeNull();
        persistedRelease.Metadata.ShouldNotBeNull();
        persistedRelease.Metadata.MetadataDatabaseClassName.ShouldBe(MetadataDatabaseClassName);
        persistedRelease.Metadata.Title.ShouldBe("Flesh+Blood");
        persistedRelease.Metadata.Description.ShouldBe("Medieval adventure");
        persistedRelease.Metadata.Genre.ShouldBe("Drama");
        persistedRelease.Metadata.CoverUrl.ShouldBe("https://images.test/flesh.jpg");
        persistedRelease.Metadata.MetadataDatabaseUrl.ShouldBe(
            "https://www.themoviedb.org/movie/12775"
        );

        persistedRelease
            .ExternalIdentifiers.Select(identifier =>
                (identifier.Type, identifier.Value, identifier.Source)
            )
            .ShouldBe(
                [
                    (ExternalIdentifierType.Imdb, "tt9999999", ExternalIdentifierSource.Xrel),
                    (ExternalIdentifierType.Tmdb, "movie/12775", ExternalIdentifierSource.Manual),
                    (ExternalIdentifierType.Imdb, "tt0089153", ExternalIdentifierSource.Manual),
                    (ExternalIdentifierType.Tmdb, "movie/12775", ExternalIdentifierSource.Tmdb),
                    (ExternalIdentifierType.Imdb, "tt0089153", ExternalIdentifierSource.Tmdb),
                ],
                ignoreOrder: true
            );

        var remainingUpload = await DbContext.ImageUploads.SingleAsync();
        remainingUpload.Id.ShouldBe(failedUpload.Id);
    }

    [Test]
    public async Task RemoveCorrectionAsync_ManualIdentifiers_RemovesThemAndResolvesMetadataAgain()
    {
        // Arrange
        var release = await AddReleaseAsync(
            "Amok.1994.1080p.BluRay.x264-GRP",
            ReleaseContentType.Movie
        );

        release.Metadata = new ReleaseMetadata
        {
            MetadataDatabaseClassName = MetadataDatabaseClassName,
            Title = "Flesh+Blood",
            Genre = "Adventure",
            CoverUrl = "https://images.test/flesh.jpg",
        };

        release.ExternalIdentifiers.AddRange([
            new ReleaseExternalIdentifier
            {
                Type = ExternalIdentifierType.Imdb,
                Value = "tt0109093",
                Source = ExternalIdentifierSource.Xrel,
            },
            new ReleaseExternalIdentifier
            {
                Type = ExternalIdentifierType.Imdb,
                Value = "tt0089153",
                Source = ExternalIdentifierSource.Manual,
            },
            new ReleaseExternalIdentifier
            {
                Type = ExternalIdentifierType.Tmdb,
                Value = "movie/12775",
                Source = ExternalIdentifierSource.Manual,
            },
        ]);

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        metadataDatabaseMock
            .Setup(database =>
                database.GetByExternalIdAsync(
                    metadataDatabaseConfig,
                    It.Is<MediaMetadataLookup>(lookup =>
                        lookup.ImdbId == "tt0109093" && lookup.TmdbId == null
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new MediaMetadata(
                    Title: "Amok",
                    Description: null,
                    Genre: null,
                    CoverUrl: "https://images.test/amok.jpg",
                    DatabaseUrl: "https://www.themoviedb.org/movie/42",
                    ImdbId: "tt0109093",
                    TmdbId: "movie/42",
                    TvdbId: null
                )
            );

        // Act
        await service.RemoveCorrectionAsync(release.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();

        var persistedRelease = await DbContext
            .Releases.Include(item => item.Metadata)
            .Include(item => item.ExternalIdentifiers)
            .SingleAsync();

        persistedRelease.MetadataCheckedAt.ShouldNotBeNull();
        persistedRelease.Metadata.ShouldNotBeNull();
        persistedRelease.Metadata.Title.ShouldBe("Amok");
        persistedRelease.Metadata.Genre.ShouldBeNull();
        persistedRelease.Metadata.CoverUrl.ShouldBe("https://images.test/amok.jpg");
        persistedRelease.ExternalIdentifiers.ShouldNotContain(identifier =>
            identifier.Source == ExternalIdentifierSource.Manual
        );

        persistedRelease.ExternalIdentifiers.ShouldContain(identifier =>
            identifier.Type == ExternalIdentifierType.Tmdb
            && identifier.Value == "movie/42"
            && identifier.Source == ExternalIdentifierSource.Tmdb
        );
    }

    private static MediaMetadata CreateFleshAndBloodMetadata()
    {
        return new MediaMetadata(
            Title: "Flesh+Blood",
            Description: "Medieval adventure",
            Genre: null,
            CoverUrl: "https://images.test/flesh.jpg",
            DatabaseUrl: "https://www.themoviedb.org/movie/12775",
            ImdbId: "tt0089153",
            TmdbId: "movie/12775",
            TvdbId: null
        );
    }

    private async Task<Release> AddReleaseAsync(string name, ReleaseContentType contentType)
    {
        var releaseGroup = new ReleaseGroup
        {
            Name = $"Release group {Guid.NewGuid():N}",
            EnableAutomaticReuploads = false,
            NumberOfHoursUntilReupload = 24,
            Releases = [],
        };

        var release = new Release
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ReleaseType = ReleaseType.Managed,
            ReleaseFolderPath = $"/tmp/{name}",
            ReleaseGroup = releaseGroup,
            ReleaseContentType = contentType,
            PrimaryLanguageCode = "de",
            ArchiveConfigs = [],
            UploadConfigs = [],
        };

        DbContext.Releases.Add(release);
        await DbContext.SaveChangesAsync();

        return release;
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LocalTimezone"] = "UTC" })
            .Build();

        return new TimeProvider(configuration);
    }
}
