using System.Text.RegularExpressions;

namespace Bearcat.Domain.UseCases.ManageReleases;

public static partial class ImdbIdParser
{
    public static IReadOnlyList<string> ExtractAll(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return ImdbIdRegex()
            .Matches(value)
            .Select(match => match.Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    [GeneratedRegex(@"tt\d{7,8}", RegexOptions.IgnoreCase)]
    private static partial Regex ImdbIdRegex();
}
