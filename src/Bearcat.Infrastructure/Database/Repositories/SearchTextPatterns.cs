namespace Bearcat.Infrastructure.Database.Repositories;

public static class SearchTextPatterns
{
    public static string? TrimOrNullWhenEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string ToLowerCaseContainsPattern(string value)
    {
        return $"%{value.ToLowerInvariant()}%";
    }

    public static int? ParseIdWithOptionalHashPrefix(string value)
    {
        return int.TryParse(value.TrimStart('#'), out var id) ? id : null;
    }
}
