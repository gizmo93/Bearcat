namespace Bearcat.MediaDatabases.Tvdb.Api;

public record TvdbSeriesExtendedRecord(
    long Id,
    string? Name,
    string? Slug,
    string? Image,
    string? Overview,
    IReadOnlyList<TvdbRemoteId>? RemoteIds
);
