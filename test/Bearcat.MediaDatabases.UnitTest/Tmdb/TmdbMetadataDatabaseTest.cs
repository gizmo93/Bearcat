using System.Net;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.MediaDatabases.Tmdb;
using Bearcat.MediaDatabases.Tmdb.Api;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.MediaDatabases.UnitTest.Tmdb;

public class TmdbMetadataDatabaseTest
{
    [Test]
    public async Task GetByExternalIdAsync_MovieFound_MapsMetadata()
    {
        var api = new Mock<ITmdbApi>(MockBehavior.Strict);
        api.Setup(item =>
                item.FindAsync(
                    "tt0109093",
                    "secret",
                    "imdb_id",
                    "de",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateApiResponse(
                    new TmdbFindResponse(
                        MovieResults:
                        [
                            new TmdbMovieResponse(
                                Id: 123,
                                Title: "Amok",
                                Overview: "Description",
                                PosterPath: "/amok.jpg",
                                ImdbId: null
                            ),
                        ],
                        TvResults: [],
                        TvEpisodeResults: []
                    )
                )
            );
        var database = new TmdbMetadataDatabase(api.Object);

        var metadata = await database.GetByExternalIdAsync(
            new TmdbConfig("secret"),
            new MediaMetadataLookup(
                MediaKind: MediaKind.Movie,
                ImdbId: "tt0109093",
                SteamAppId: null,
                TmdbId: null,
                TvdbIdOrSlug: null,
                Title: "Amok",
                Year: 1994,
                SeasonNumber: null,
                EpisodeNumber: null,
                LanguageCode: "de"
            )
        );

        metadata.ShouldNotBeNull();
        metadata.Title.ShouldBe("Amok");
        metadata.Description.ShouldBe("Description");
        metadata.CoverUrl.ShouldBe("https://image.tmdb.org/t/p/w500/amok.jpg");
        metadata.DatabaseUrl.ShouldBe("https://www.themoviedb.org/movie/123");
    }

    [Test]
    public async Task GetByTitleAsync_TvEpisode_SearchesSeries()
    {
        var api = new Mock<ITmdbApi>(MockBehavior.Strict);
        api.Setup(item =>
                item.SearchTvAsync(
                    "secret",
                    "Agent Kim Reactivated",
                    null,
                    null,
                    false,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                CreateApiResponse(
                    new TmdbSearchResponse<TmdbTvResponse>([
                        new TmdbTvResponse(
                            Id: 456,
                            Name: "Agent Kim Reactivated",
                            Overview: null,
                            PosterPath: "/agent.jpg",
                            ExternalIds: null
                        ),
                    ])
                )
            );
        var database = new TmdbMetadataDatabase(api.Object);

        var metadata = await database.GetByTitleAsync(
            new TmdbConfig("secret"),
            new MediaMetadataLookup(
                MediaKind: MediaKind.TvEpisode,
                ImdbId: null,
                SteamAppId: null,
                TmdbId: null,
                TvdbIdOrSlug: null,
                Title: "Agent Kim Reactivated",
                Year: null,
                SeasonNumber: 1,
                EpisodeNumber: 5,
                LanguageCode: null
            )
        );

        metadata.ShouldNotBeNull();
        metadata.Title.ShouldBe("Agent Kim Reactivated");
        metadata.CoverUrl.ShouldBe("https://image.tmdb.org/t/p/w500/agent.jpg");
        metadata.DatabaseUrl.ShouldBe("https://www.themoviedb.org/tv/456");
    }

    [Test]
    public async Task GetByDatabaseIdAsync_MovieId_ReturnsMovieWithImdbIdAndTmdbId()
    {
        // Arrange
        var api = new Mock<ITmdbApi>(MockBehavior.Strict);

        api.Setup(item => item.GetMovieAsync(12775, "secret", "de", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateApiResponse(
                    new TmdbMovieResponse(
                        Id: 12775,
                        Title: "Flesh+Blood",
                        Overview: "Description",
                        PosterPath: "/flesh.jpg",
                        ImdbId: "tt0089153"
                    )
                )
            );

        var database = new TmdbMetadataDatabase(api.Object);

        // Act
        var metadata = await database.GetByDatabaseIdAsync(
            new TmdbConfig("secret"),
            CreateLookup(MediaKind.Movie, tmdbId: "movie/12775")
        );

        // Assert
        metadata.ShouldNotBeNull();
        metadata.Title.ShouldBe("Flesh+Blood");
        metadata.DatabaseUrl.ShouldBe("https://www.themoviedb.org/movie/12775");
        metadata.ImdbId.ShouldBe("tt0089153");
        metadata.TmdbId.ShouldBe("movie/12775");
        metadata.TvdbId.ShouldBeNull();
    }

    [Test]
    public async Task GetByDatabaseIdAsync_TvIdForEpisode_ReturnsShowWithImdbIdFromExternalIds()
    {
        // Arrange
        var api = new Mock<ITmdbApi>(MockBehavior.Strict);

        api.Setup(item =>
                item.GetTvAsync(1399, "secret", "de", "external_ids", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                CreateApiResponse(
                    new TmdbTvResponse(
                        Id: 1399,
                        Name: "Game of Thrones",
                        Overview: "Description",
                        PosterPath: "/got.jpg",
                        ExternalIds: new TmdbExternalIdsResponse("tt0944947")
                    )
                )
            );

        var database = new TmdbMetadataDatabase(api.Object);

        // Act
        var metadata = await database.GetByDatabaseIdAsync(
            new TmdbConfig("secret"),
            CreateLookup(MediaKind.TvEpisode, tmdbId: "tv/1399")
        );

        // Assert
        metadata.ShouldNotBeNull();
        metadata.Title.ShouldBe("Game of Thrones");
        metadata.DatabaseUrl.ShouldBe("https://www.themoviedb.org/tv/1399");
        metadata.ImdbId.ShouldBe("tt0944947");
        metadata.TmdbId.ShouldBe("tv/1399");
    }

    private static MediaMetadataLookup CreateLookup(MediaKind mediaKind, string tmdbId)
    {
        return new MediaMetadataLookup(
            MediaKind: mediaKind,
            ImdbId: null,
            SteamAppId: null,
            TmdbId: tmdbId,
            TvdbIdOrSlug: null,
            Title: "Ignored title",
            Year: null,
            SeasonNumber: 1,
            EpisodeNumber: 2,
            LanguageCode: "de"
        );
    }

    private static ApiResponse<T> CreateApiResponse<T>(T content)
    {
        return new ApiResponse<T>(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(),
            },
            content,
            new RefitSettings(),
            error: null
        );
    }
}
