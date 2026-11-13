using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.Shared.MediaMetadataResolution;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared.MediaMetadataResolution;

public class MediaMetadataResolverTest
{
    [Test]
    public async Task ResolveAsync_LookupWithTmdbIdAndNoMatch_ReturnsNullWithoutExternalIdOrTitleFallback()
    {
        // Arrange
        const string serializedConfig = "{}";
        var config = new Mock<IMediaMetadataDatabaseConfig>(MockBehavior.Strict).Object;
        var database = new Mock<IMediaMetadataDatabase>();
        database.SetupGet(item => item.SupportedMediaKinds).Returns([MediaKind.Movie]);
        database.SetupGet(item => item.ResolutionPriority).Returns(0);
        database.Setup(item => item.DeserializeConfig(serializedConfig)).Returns(config);

        database
            .Setup(item =>
                item.GetByDatabaseIdAsync(
                    config,
                    It.Is<MediaMetadataLookup>(lookup => lookup.TmdbId == "movie/12775"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((MediaMetadata?)null);

        var repository = new Mock<IMediaMetadataResolverRepository>(MockBehavior.Strict);

        repository
            .Setup(item => item.GetActiveRegistrationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MediaMetadataDatabaseRegistration("TmdbMetadataDatabase", serializedConfig),
            ]);

        var databaseFactory = new Mock<IMediaMetadataDatabaseFactory>(MockBehavior.Strict);
        databaseFactory.Setup(item => item.Get("TmdbMetadataDatabase")).Returns(database.Object);

        var resolver = new MediaMetadataResolver(
            repository.Object,
            databaseFactory.Object,
            NullLogger<MediaMetadataResolver>.Instance
        );

        // Act
        var resolved = await resolver.ResolveAsync(
            new MediaMetadataLookup(
                MediaKind: MediaKind.Movie,
                ImdbId: "tt0089153",
                SteamAppId: null,
                TmdbId: "movie/12775",
                TvdbIdOrSlug: null,
                Title: "Flesh and Blood",
                Year: 1985,
                SeasonNumber: null,
                EpisodeNumber: null,
                LanguageCode: "de"
            )
        );

        // Assert
        resolved.ShouldBeNull();

        database.Verify(
            item =>
                item.GetByDatabaseIdAsync(
                    config,
                    It.IsAny<MediaMetadataLookup>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        database.Verify(
            item =>
                item.GetByExternalIdAsync(
                    It.IsAny<IMediaMetadataDatabaseConfig>(),
                    It.IsAny<MediaMetadataLookup>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );

        database.Verify(
            item =>
                item.GetByTitleAsync(
                    It.IsAny<IMediaMetadataDatabaseConfig>(),
                    It.IsAny<MediaMetadataLookup>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
