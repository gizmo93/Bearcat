using System.Text.RegularExpressions;
using Bearcat.Domain.Shared;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReleaseNameParsing;

public static partial class ReleaseNameParser
{
    private static readonly char[] TokenSeparators = ['.', '_', ' '];

    private static readonly Dictionary<string, ReleaseResolution> Resolutions = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["2160p"] = ReleaseResolution.R2160p,
        ["2160i"] = ReleaseResolution.R2160p,
        ["4k"] = ReleaseResolution.R2160p,
        ["uhd"] = ReleaseResolution.R2160p,
        ["1080p"] = ReleaseResolution.R1080p,
        ["1080i"] = ReleaseResolution.R1080p,
        ["720p"] = ReleaseResolution.R720p,
        ["720i"] = ReleaseResolution.R720p,
        ["576p"] = ReleaseResolution.R576p,
        ["480p"] = ReleaseResolution.R480p,
    };

    private static readonly HashSet<string> MultiLanguageMarkers = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "multi",
        "dl",
        "dual",
        "ml",
    };

    private static readonly Dictionary<string, ReleaseSource> Sources = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["bluray"] = ReleaseSource.BluRay,
        ["bdrip"] = ReleaseSource.BdRip,
        ["brrip"] = ReleaseSource.BrRip,
        ["web-dl"] = ReleaseSource.WebDl,
        ["webdl"] = ReleaseSource.WebDl,
        ["web"] = ReleaseSource.Web,
        ["webrip"] = ReleaseSource.WebRip,
        ["hdtv"] = ReleaseSource.Hdtv,
        ["hdtvrip"] = ReleaseSource.HdtvRip,
        ["pdtv"] = ReleaseSource.Pdtv,
        ["dvdrip"] = ReleaseSource.DvdRip,
        ["dvdr"] = ReleaseSource.DvdR,
        ["hdrip"] = ReleaseSource.HdRip,
        ["tvrip"] = ReleaseSource.TvRip,
        ["satrip"] = ReleaseSource.SatRip,
    };

    public static ParsedReleaseName Parse(string releaseName)
    {
        var (body, group) = ExtractGroup(releaseName.Trim());
        var tokens = body.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries);

        var resolution = ReleaseResolution.Unknown;
        string? language = null;
        var isMultiLanguage = false;
        var source = ReleaseSource.Unknown;
        int? year = null;
        int? season = null;
        int? episode = null;
        int? episodeEnd = null;
        int? firstAnchorIndex = null;

        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            var isAnchor = true;

            if (Resolutions.TryGetValue(token, out var parsedResolution))
            {
                if (resolution == ReleaseResolution.Unknown)
                {
                    resolution = parsedResolution;
                }
            }
            else if (LanguageCatalog.TryResolveName(token, out var parsedLanguage))
            {
                language ??= parsedLanguage;
            }
            else if (MultiLanguageMarkers.Contains(token))
            {
                isMultiLanguage = true;
            }
            else if (Sources.TryGetValue(token, out var parsedSource))
            {
                if (source == ReleaseSource.Unknown)
                {
                    source = parsedSource;
                }
            }
            else if (
                TryParseSeasonEpisode(
                    token,
                    out var parsedSeason,
                    out var parsedEpisode,
                    out var parsedEpisodeEnd
                )
            )
            {
                season ??= parsedSeason;
                episode ??= parsedEpisode;
                episodeEnd ??= parsedEpisodeEnd;
            }
            else if (index > 0 && IsYear(token))
            {
                year ??= int.Parse(token);
            }
            else
            {
                isAnchor = false;
            }

            if (isAnchor && firstAnchorIndex is null && index > 0)
            {
                firstAnchorIndex = index;
            }
        }

        var titleTokenCount = firstAnchorIndex ?? tokens.Length;

        return new ParsedReleaseName
        {
            Title = string.Join(' ', tokens.Take(titleTokenCount)),
            Year = year,
            Season = season,
            Episode = episode,
            EpisodeEnd = episodeEnd,
            Language = language,
            IsMultiLanguage = isMultiLanguage,
            Resolution = resolution,
            Source = source,
            Group = group,
        };
    }

    private static (string Body, string? Group) ExtractGroup(string name)
    {
        var dashIndex = name.LastIndexOf('-');

        if (dashIndex <= 0 || dashIndex == name.Length - 1)
        {
            return (name, null);
        }

        var candidate = name[(dashIndex + 1)..];

        if (!GroupPattern().IsMatch(candidate))
        {
            return (name, null);
        }

        return (name[..dashIndex], candidate);
    }

    private static bool TryParseSeasonEpisode(
        string token,
        out int season,
        out int? episode,
        out int? episodeEnd
    )
    {
        season = 0;
        episode = null;
        episodeEnd = null;

        var match = SeasonEpisodePattern().Match(token);

        if (!match.Success)
        {
            return false;
        }

        season = int.Parse(match.Groups["season"].Value);

        if (match.Groups["episode"].Success)
        {
            episode = int.Parse(match.Groups["episode"].Value);
        }

        if (match.Groups["episodeEnd"].Success)
        {
            episodeEnd = int.Parse(match.Groups["episodeEnd"].Value);
        }

        return true;
    }

    private static bool IsYear(string token)
    {
        return token.Length == 4
            && (
                token.StartsWith("19", StringComparison.Ordinal)
                || token.StartsWith("20", StringComparison.Ordinal)
            )
            && int.TryParse(token, out _);
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.]{0,29}$")]
    private static partial Regex GroupPattern();

    [GeneratedRegex(
        @"^S(?<season>\d{1,2})(?:E(?<episode>\d{1,3})(?:-?E(?<episodeEnd>\d{1,3}))?)?$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex SeasonEpisodePattern();
}
