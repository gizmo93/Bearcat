using System.Globalization;
using System.Text.RegularExpressions;

namespace Bearcat.Website.Pages.ManageReleaseCollections.Overview;

public static partial class ReleaseCollectionEpisodeLabel
{
    public static IReadOnlyList<string?> GetEpisodeLabels(IReadOnlyList<string> releaseNames)
    {
        var seasonAndEpisodeMatches = releaseNames
            .Select(releaseName => SeasonAndEpisodeRegex().Match(releaseName))
            .ToList();

        var distinctSeasonCount = seasonAndEpisodeMatches
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups["season"].Value, CultureInfo.InvariantCulture))
            .Distinct()
            .Count();

        var isSeasonShown = distinctSeasonCount > 1;

        return seasonAndEpisodeMatches
            .Select(match =>
                match.Success ? CreateEpisodeLabel(match, isSeasonShown) : (string?)null
            )
            .ToList();
    }

    private static string CreateEpisodeLabel(Match match, bool isSeasonShown) =>
        isSeasonShown
            ? $"S{match.Groups["season"].Value}E{match.Groups["episode"].Value}"
            : $"E{match.Groups["episode"].Value}";

    [GeneratedRegex(@"S(?<season>\d{1,3})E(?<episode>\d{1,4})", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonAndEpisodeRegex();
}
