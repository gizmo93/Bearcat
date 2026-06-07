using System.Text.RegularExpressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections;

public static partial class ReleaseCollectionDetectionService
{
    private const string DefaultSeriesCollectionTemplate = "{title}.S{season}.{variant}";
    private static readonly char[] ReleaseNamePartTrimCharacters = ['.', '_', '-', ' '];

    public static ReleaseCollectionDetectionResult? Detect(
        string releaseName,
        ReleaseTemplate releaseTemplate
    )
    {
        if (
            !releaseTemplate.UseReleaseCollections
            || releaseTemplate.ReleaseCollectionDetectionMode
                is ReleaseCollectionDetectionMode.Disabled
            || string.IsNullOrWhiteSpace(releaseName)
        )
        {
            return null;
        }

        var trimmedReleaseName = releaseName.Trim();

        return releaseTemplate.ReleaseCollectionDetectionMode switch
        {
            ReleaseCollectionDetectionMode.SeriesEpisodePattern => DetectSeriesEpisode(
                trimmedReleaseName,
                releaseTemplate
            ),
            ReleaseCollectionDetectionMode.CustomRegex => DetectCustomRegex(
                trimmedReleaseName,
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
        var match = SeriesEpisodeRegex().Match(releaseName);

        if (!match.Success)
        {
            return null;
        }

        var title = match.Groups["title"].Value.Trim(ReleaseNamePartTrimCharacters);
        var season = match.Groups["season"].Value.PadLeft(2, '0');
        var variant = match.Groups["rest"].Value.Trim(ReleaseNamePartTrimCharacters);

        var templateValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = title,
            ["title:dotToSpace"] = ReplaceDotsWithSpaces(title),
            ["season"] = season,
            ["variant"] = variant,
        };

        var keyTemplate = string.IsNullOrWhiteSpace(releaseTemplate.ReleaseCollectionKeyTemplate)
            ? DefaultSeriesCollectionTemplate
            : releaseTemplate.ReleaseCollectionKeyTemplate;

        var nameTemplate = string.IsNullOrWhiteSpace(releaseTemplate.ReleaseCollectionNameTemplate)
            ? DefaultSeriesCollectionTemplate
            : releaseTemplate.ReleaseCollectionNameTemplate;

        var key = RenderTemplate(keyTemplate, templateValues);
        var name = RenderTemplate(nameTemplate, templateValues);

        return new ReleaseCollectionDetectionResult(
            Key: NormalizeKey(key),
            Name: NormalizeSpaces(name)
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

        var match = MatchCustomPattern(releaseName, releaseTemplate.ReleaseCollectionPattern);
        if (match is null)
        {
            return null;
        }

        var templateValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var groupName in match.Groups.Keys)
        {
            if (int.TryParse(groupName, out _))
            {
                continue;
            }

            var groupValue = match.Groups[groupName].Value;
            templateValues[groupName] = groupValue;
            templateValues[$"{groupName}:dotToSpace"] = ReplaceDotsWithSpaces(groupValue);
        }

        var key = RenderTemplate(
            releaseTemplate.ReleaseCollectionKeyTemplate,
            templateValues
        );
        var name = RenderTemplate(
            releaseTemplate.ReleaseCollectionNameTemplate,
            templateValues
        );

        return new ReleaseCollectionDetectionResult(NormalizeKey(key), NormalizeSpaces(name));
    }

    private static string RenderTemplate(
        string template,
        IReadOnlyDictionary<string, string> templateValues
    )
    {
        return TemplateTokenRegex().Replace(
            template,
            match =>
            {
                var token = match.Groups["token"].Value;
                return templateValues.TryGetValue(token, out var value) ? value : string.Empty;
            }
        );
    }

    private static Match? MatchCustomPattern(string releaseName, string pattern)
    {
        try
        {
            var match = Regex.Match(
                releaseName,
                pattern,
                RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(250)
            );

            return match.Success ? match : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    private static string ReplaceDotsWithSpaces(string value)
    {
        var textWithSpaces = value.Replace('.', ' ').Replace('_', ' ');
        return NormalizeSpaces(textWithSpaces);
    }

    private static string NormalizeSpaces(string value)
    {
        var trimmedValue = value.Trim();
        return SpaceRegex().Replace(trimmedValue, " ");
    }

    private static string NormalizeKey(string value)
    {
        var normalizedSeparators = KeySeparatorRegex()
            .Replace(value.Trim().ToLowerInvariant(), ".")
            .Trim('.');
        return SpaceRegex().Replace(normalizedSeparators, ".");
    }

    [GeneratedRegex(
        @"^(?<title>.+?)[._ -]+S(?<season>\d{1,2})E(?<episode>\d{1,3})(?<rest>.*)$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex SeriesEpisodeRegex();

    [GeneratedRegex(@"\{(?<token>[^{}]+)\}")]
    private static partial Regex TemplateTokenRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    [GeneratedRegex(@"[._\s-]+")]
    private static partial Regex KeySeparatorRegex();
}
