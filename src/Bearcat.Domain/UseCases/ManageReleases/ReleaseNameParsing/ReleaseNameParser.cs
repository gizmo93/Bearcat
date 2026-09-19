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

    private static readonly Dictionary<string, ReleasePlatform> Platforms = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["pc"] = ReleasePlatform.Windows,
        ["windows"] = ReleasePlatform.Windows,
        ["macos"] = ReleasePlatform.MacOs,
        ["macosx"] = ReleasePlatform.MacOs,
        ["osx"] = ReleasePlatform.MacOs,
        ["linux"] = ReleasePlatform.Linux,
        ["amiga"] = ReleasePlatform.Amiga,
        ["nes"] = ReleasePlatform.Nes,
        ["snes"] = ReleasePlatform.Snes,
        ["zxs"] = ReleasePlatform.ZxSpectrum,
        ["c64"] = ReleasePlatform.Commodore64,
        ["ps3"] = ReleasePlatform.PlayStation3,
        ["ps4"] = ReleasePlatform.PlayStation4,
        ["ps5"] = ReleasePlatform.PlayStation5,
        ["x360"] = ReleasePlatform.Xbox360,
        ["xbox360"] = ReleasePlatform.Xbox360,
        ["xboxone"] = ReleasePlatform.XboxOne,
        ["xboxsx"] = ReleasePlatform.XboxSeries,
        ["nsw"] = ReleasePlatform.NintendoSwitch,
    };

    private static readonly HashSet<string> GameMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "update",
        "dlc",
        "trainer",
        "gog",
        "drmfree",
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
        var platform = ReleasePlatform.Unknown;
        var hasGameMarkers = false;
        int? year = null;
        int? season = null;
        int? episode = null;
        int? episodeEnd = null;
        int? firstAnchorIndex = null;

        var hasResolutionOrSourceToken = tokens.Any(token =>
            Resolutions.ContainsKey(token) || Sources.ContainsKey(token)
        );

        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            var nextToken = index + 1 < tokens.Length ? tokens[index + 1] : null;
            var isAnchor = true;

            if (Resolutions.TryGetValue(token, out var parsedResolution))
            {
                if (resolution == ReleaseResolution.Unknown)
                {
                    resolution = parsedResolution;
                }
            }
            else if (Platforms.TryGetValue(token, out var parsedPlatform))
            {
                if (platform == ReleasePlatform.Unknown)
                {
                    platform = parsedPlatform;
                }
            }
            else if (LanguageCatalog.TryResolveName(token, out var parsedLanguage))
            {
                language ??= parsedLanguage;
            }
            else if (IsMultiLanguageMarker(token))
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
            else if (IsGameMarker(token, nextToken))
            {
                hasGameMarkers = true;
                isAnchor = !hasResolutionOrSourceToken;
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
            Platform = platform,
            HasGameMarkers = hasGameMarkers,
            Group = group,
        };
    }

    private static bool IsMultiLanguageMarker(string token)
    {
        return MultiLanguageMarkers.Contains(token) || MultiLanguagePattern().IsMatch(token);
    }

    private static bool IsGameMarker(string token, string? nextToken)
    {
        if (GameMarkers.Contains(token) || VersionPattern().IsMatch(token))
        {
            return true;
        }

        if (
            token.Equals("build", StringComparison.OrdinalIgnoreCase)
            && nextToken is not null
            && nextToken.All(char.IsAsciiDigit)
        )
        {
            return true;
        }

        return token.Equals("early", StringComparison.OrdinalIgnoreCase)
            && nextToken?.Equals("access", StringComparison.OrdinalIgnoreCase) == true;
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

    [GeneratedRegex(@"^multi\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex MultiLanguagePattern();

    [GeneratedRegex(@"^v\d+(?:\.\d+)*[a-z]?$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.]{0,29}$")]
    private static partial Regex GroupPattern();

    [GeneratedRegex(
        @"^S(?<season>\d{1,2})(?:E(?<episode>\d{1,3})(?:-?E(?<episodeEnd>\d{1,3}))?)?$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex SeasonEpisodePattern();
}
