using System.Text.RegularExpressions;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Parsers;

public static partial class MediaDatabaseReferenceParser
{
    public static MediaDatabaseReference? Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmedInput = input.Trim();

        var tmdbMatch = TmdbUrlRegex().Match(trimmedInput);

        if (tmdbMatch.Success)
        {
            return new MediaDatabaseReference(
                Type: ExternalIdentifierType.Tmdb,
                Value: $"{tmdbMatch.Groups["kind"].Value.ToLowerInvariant()}/{tmdbMatch.Groups["id"].Value}"
            );
        }

        var tvdbIdMatch = TvdbIdUrlRegex().Match(trimmedInput);

        if (tvdbIdMatch.Success)
        {
            return new MediaDatabaseReference(
                Type: ExternalIdentifierType.Tvdb,
                Value: tvdbIdMatch.Groups["id"].Value
            );
        }

        var tvdbSlugMatch = TvdbSlugUrlRegex().Match(trimmedInput);

        if (tvdbSlugMatch.Success)
        {
            return new MediaDatabaseReference(
                Type: ExternalIdentifierType.Tvdb,
                Value: tvdbSlugMatch.Groups["slug"].Value
            );
        }

        var steamMatch = SteamAppUrlRegex().Match(trimmedInput);

        if (steamMatch.Success)
        {
            return new MediaDatabaseReference(
                Type: ExternalIdentifierType.Steam,
                Value: steamMatch.Groups["id"].Value
            );
        }

        var imdbIds = ImdbIdParser.ExtractAll(trimmedInput);

        if (
            imdbIds.Count > 0
            && (
                trimmedInput.Equals(imdbIds[0], StringComparison.OrdinalIgnoreCase)
                || trimmedInput.Contains("imdb.com/", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return new MediaDatabaseReference(Type: ExternalIdentifierType.Imdb, Value: imdbIds[0]);
        }

        return null;
    }

    [GeneratedRegex(@"themoviedb\.org/(?<kind>movie|tv)/(?<id>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TmdbUrlRegex();

    [GeneratedRegex(@"thetvdb\.com/dereferrer/series/(?<id>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TvdbIdUrlRegex();

    [GeneratedRegex(@"thetvdb\.com/series/(?<slug>[a-z0-9-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex TvdbSlugUrlRegex();

    [GeneratedRegex(
        @"(?:store\.steampowered\.com|steamdb\.info)/app/(?<id>\d+)",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex SteamAppUrlRegex();
}
