using System.Text;
using System.Text.RegularExpressions;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections;

public static partial class ReleaseCollectionDetectionService
{
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
        var match = SeriesEpisodeRegex().Match(releaseName.Trim());

        if (!match.Success)
        {
            return null;
        }

        var title = match.Groups["title"].Value.Trim('.', '_', '-', ' ');
        var season = match.Groups["season"].Value.PadLeft(2, '0');
        var variant = match.Groups["rest"].Value.Trim('.', '_', '-', ' ');

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
            ? "{title}.S{season}.{variant}"
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

        Match match;

        try
        {
            match = Regex.Match(
                releaseName.Trim(),
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

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    [GeneratedRegex(@"[._\s-]+")]
    private static partial Regex KeySeparatorRegex();
}
