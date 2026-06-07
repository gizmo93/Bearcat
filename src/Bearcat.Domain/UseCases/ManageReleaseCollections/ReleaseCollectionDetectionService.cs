using System.Text;
using System.Text.RegularExpressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections;

public static partial class ReleaseCollectionDetectionService
{
    private static readonly IReadOnlySet<string> Languages = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "Deutsch",
        "English",
        "Englisch",
        "French",
        "German",
        "Italian",
        "Multi",
        "Spanish",
    };

    private static readonly IReadOnlySet<string> VideoExtensions = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ".avi",
        ".m2ts",
        ".m4v",
        ".mkv",
        ".mov",
        ".mp4",
        ".mpeg",
        ".mpg",
        ".ts",
        ".wmv",
    };

    public static ReleaseCollectionDetectionResult? Detect(
        string releaseName,
        ReleaseTemplate releaseTemplate
    )
    {
        if (
            !releaseTemplate.UseReleaseCollections
            || releaseTemplate.ReleaseCollectionDetectionMode is ReleaseCollectionDetectionMode.Disabled
            || string.IsNullOrWhiteSpace(releaseName)
        )
        {
            return null;
        }

        return releaseTemplate.ReleaseCollectionDetectionMode switch
        {
            ReleaseCollectionDetectionMode.SeriesEpisodePattern => DetectSeriesEpisode(
                releaseName,
                releaseTemplate
            ),
            ReleaseCollectionDetectionMode.CustomRegex => DetectCustomRegex(
                releaseName,
                releaseTemplate
            ),
            _ => null,
        };
    }

    private static ReleaseCollectionDetectionResult? DetectSeriesEpisode(
        string releaseName,
        ReleaseTemplate releaseTemplate
    )
    {
        var cleanReleaseName = StripKnownVideoExtension(releaseName.Trim());
        var match = SeriesEpisodeRegex().Match(cleanReleaseName);

        if (!match.Success)
        {
            return null;
        }

        var title = match.Groups["title"].Value.Trim('.', '_', '-', ' ');
        var season = match.Groups["season"].Value.PadLeft(2, '0');
        var variant = match.Groups["rest"].Value.Trim('.', '_', '-', ' ');

        if (releaseTemplate.IgnoreLanguageInReleaseCollectionName)
        {
            variant = RemoveLeadingLanguage(variant);
        }

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = title,
            ["title:dotToSpace"] = NormalizeDisplayTitle(title),
            ["season"] = season,
            ["variant"] = variant,
        };

        var keyTemplate = string.IsNullOrWhiteSpace(releaseTemplate.ReleaseCollectionKeyTemplate)
            ? "{title}.S{season}.{variant}"
            : releaseTemplate.ReleaseCollectionKeyTemplate;
        var nameTemplate = string.IsNullOrWhiteSpace(releaseTemplate.ReleaseCollectionNameTemplate)
            ? "{title:dotToSpace} S{season} {variant}"
            : releaseTemplate.ReleaseCollectionNameTemplate;

        return new ReleaseCollectionDetectionResult(
            NormalizeKey(RenderTemplate(keyTemplate, replacements)),
            NormalizeSpaces(RenderTemplate(nameTemplate, replacements))
        );
    }

    private static ReleaseCollectionDetectionResult? DetectCustomRegex(
        string releaseName,
        ReleaseTemplate releaseTemplate
    )
    {
        if (
            string.IsNullOrWhiteSpace(releaseTemplate.ReleaseCollectionPattern)
            || string.IsNullOrWhiteSpace(releaseTemplate.ReleaseCollectionKeyTemplate)
            || string.IsNullOrWhiteSpace(releaseTemplate.ReleaseCollectionNameTemplate)
        )
        {
            return null;
        }

        var cleanReleaseName = StripKnownVideoExtension(releaseName.Trim());
        Match match;

        try
        {
            match = Regex.Match(
                cleanReleaseName,
                releaseTemplate.ReleaseCollectionPattern,
                RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(250)
            );
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        if (!match.Success)
        {
            return null;
        }

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var groupName in match.Groups.Keys)
        {
            if (int.TryParse(groupName, out _))
            {
                continue;
            }

            replacements[groupName] = match.Groups[groupName].Value;
            replacements[$"{groupName}:dotToSpace"] = NormalizeDisplayTitle(
                match.Groups[groupName].Value
            );
        }

        return new ReleaseCollectionDetectionResult(
            NormalizeKey(RenderTemplate(releaseTemplate.ReleaseCollectionKeyTemplate, replacements)),
            NormalizeSpaces(
                RenderTemplate(releaseTemplate.ReleaseCollectionNameTemplate, replacements)
            )
        );
    }

    private static string RenderTemplate(
        string template,
        IReadOnlyDictionary<string, string> replacements
    )
    {
        var result = new StringBuilder(template.Length);
        var position = 0;

        foreach (Match match in TemplateTokenRegex().Matches(template))
        {
            result.Append(template.AsSpan(position, match.Index - position));
            var token = match.Groups["token"].Value;
            result.Append(replacements.GetValueOrDefault(token, string.Empty));
            position = match.Index + match.Length;
        }

        result.Append(template.AsSpan(position));
        return result.ToString();
    }

    private static string StripKnownVideoExtension(string releaseName)
    {
        var extension = Path.GetExtension(releaseName);

        return VideoExtensions.Contains(extension)
            ? releaseName[..^extension.Length]
            : releaseName;
    }

    private static string RemoveLeadingLanguage(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var match = LeadingTokenRegex().Match(value);

        if (!match.Success || !Languages.Contains(match.Groups["token"].Value))
        {
            return value;
        }

        return value[match.Length..].Trim('.', '_', '-', ' ');
    }

    private static string NormalizeDisplayTitle(string value)
    {
        return NormalizeSpaces(value.Replace('.', ' ').Replace('_', ' '));
    }

    private static string NormalizeSpaces(string value)
    {
        return SpaceRegex().Replace(value.Trim(), " ");
    }

    private static string NormalizeKey(string value)
    {
        var key = KeySeparatorRegex()
            .Replace(value.Trim().ToLowerInvariant(), ".")
            .Trim('.');

        return SpaceRegex().Replace(key, ".");
    }

    [GeneratedRegex(@"^(?<title>.+?)[._ -]+S(?<season>\d{1,2})E(?<episode>\d{1,3})(?<rest>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex SeriesEpisodeRegex();

    [GeneratedRegex(@"\{(?<token>[^{}]+)\}")]
    private static partial Regex TemplateTokenRegex();

    [GeneratedRegex(@"^(?<token>[^._ -]+)[._ -]+")]
    private static partial Regex LeadingTokenRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    [GeneratedRegex(@"[._\s-]+")]
    private static partial Regex KeySeparatorRegex();

}
