using System.Text.Json.Serialization;

namespace Bearcat.MediaDatabases.Tvdb.Api;

public record TvdbResponse<T>(T? Data, string? Status);

public record TvdbLoginRequest([property: JsonPropertyName("apikey")] string Apikey);

public record TvdbLoginData(string Token);

public record TvdbRemoteIdResult(TvdbSeriesBaseRecord? Series);

public record TvdbSeriesBaseRecord(
    long Id,
    string? Name,
    string? Slug,
    string? Image,
    string? Overview
);

public record TvdbSearchResult(
    [property: JsonPropertyName("tvdb_id")] string? TvdbId,
    string? Name,
    string? Overview,
    string? Slug,
    [property: JsonPropertyName("image_url")] string? ImageUrl
);

public record TvdbTranslation(string? Name, string? Overview);
