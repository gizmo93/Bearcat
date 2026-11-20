namespace Bearcat.Infrastructure.Database.Repositories;

public static class SearchTextPatterns
{
    public const string LikeEscapeCharacter = "\\";

    public static string? TrimOrNullWhenEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string ToContainsPattern(string value)
    {
        return $"%{value}%";
    }

    public static string ToLowerCaseContainsPattern(string value)
    {
        return $"%{value.ToLowerInvariant()}%";
    }

    public static string ToLowerCaseWildcardPattern(string value)
    {
        var escapedValue = value
            .ToLowerInvariant()
            .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter)
            .Replace("%", LikeEscapeCharacter + "%")
            .Replace("_", LikeEscapeCharacter + "_")
            .Replace('*', '%');

        return value.Contains('*') ? escapedValue : $"%{escapedValue}%";
    }

    public static int? ParseIdWithOptionalHashPrefix(string value)
    {
        return int.TryParse(value.TrimStart('#'), out var id) ? id : null;
    }
}
