namespace Bearcat.Domain.Shared;

public static class StringExtensions
{
    public static string? TrimOrNullWhenWhitespace(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
