using System.Text.RegularExpressions;

namespace Bearcat.Domain.UseCases.ManageReleases.Parsers;

public static partial class SteamAppIdParser
{
    public static IReadOnlyList<string> ExtractAll(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return SteamAppIdRegex()
            .Matches(value)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    [GeneratedRegex(@"store\.steampowered\.com/app/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SteamAppIdRegex();
}
