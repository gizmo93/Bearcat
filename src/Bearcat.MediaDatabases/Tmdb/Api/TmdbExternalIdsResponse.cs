using System.Text.Json.Serialization;

namespace Bearcat.MediaDatabases.Tmdb.Api;

public record TmdbExternalIdsResponse([property: JsonPropertyName("imdb_id")] string? ImdbId);
