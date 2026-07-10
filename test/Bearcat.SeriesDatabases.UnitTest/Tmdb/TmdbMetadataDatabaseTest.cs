using System.Net;
using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.SeriesDatabases.Tmdb;
using Bearcat.SeriesDatabases.Tmdb.Api;
using Moq;
using Refit;
using Shouldly;

namespace Bearcat.SeriesDatabases.UnitTest.Tmdb;

public class TmdbMetadataDatabaseTest
{
    [Test]
    public async Task GetByImdbIdAsync_MovieFound_MapsMetadata()
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
                            new TmdbMovieResponse(123, "Amok", "Description", "/amok.jpg"),
                        ],
                        TvResults: [],
                        TvEpisodeResults: []
                    )
                )
            );
        var database = new TmdbMetadataDatabase(api.Object);

        var metadata = await database.GetByImdbIdAsync(
            new TmdbConfig("secret"),
            new MediaMetadataLookup(MediaKind.Movie, "tt0109093", "Amok", 1994, null, null, "de")
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
                        new TmdbTvResponse(456, "Agent Kim Reactivated", null, "/agent.jpg"),
                    ])
                )
            );
        var database = new TmdbMetadataDatabase(api.Object);

        var metadata = await database.GetByTitleAsync(
            new TmdbConfig("secret"),
            new MediaMetadataLookup(
                MediaKind.TvEpisode,
                null,
                "Agent Kim Reactivated",
                null,
                1,
                5,
                null
            )
        );

        metadata.ShouldNotBeNull();
        metadata.Title.ShouldBe("Agent Kim Reactivated");
        metadata.CoverUrl.ShouldBe("https://image.tmdb.org/t/p/w500/agent.jpg");
        metadata.DatabaseUrl.ShouldBe("https://www.themoviedb.org/tv/456");
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
