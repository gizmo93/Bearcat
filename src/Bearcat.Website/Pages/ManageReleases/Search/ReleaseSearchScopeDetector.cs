using System.Globalization;
using System.Text.RegularExpressions;

namespace Bearcat.Website.Pages.ManageReleases.Search;

public static partial class ReleaseSearchScopeDetector
{
    public static IReadOnlyList<ReleaseSearchScope> GetMatchingScopes(string text)
    {
        var trimmedText = text.Trim();
        var matchingScopes = new List<ReleaseSearchScope>();

        if (IsUrl(trimmedText))
        {
            matchingScopes.Add(ReleaseSearchScope.DownloadLink);
            matchingScopes.Add(ReleaseSearchScope.PostedLocation);
        }

        if (IsArchiveFileName(trimmedText))
        {
            matchingScopes.Add(ReleaseSearchScope.ArchiveFile);
        }

        if (IsUploadId(trimmedText))
        {
            matchingScopes.Add(ReleaseSearchScope.UploadId);
        }

        return matchingScopes;
    }

    private static bool IsUrl(string text) =>
        text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static bool IsArchiveFileName(string text) =>
        !text.Any(char.IsWhiteSpace)
        && (ArchiveFileExtensionRegex().IsMatch(text) || ArchivePartNumberRegex().IsMatch(text));

    private static bool IsUploadId(string text)
    {
        var digits = text.StartsWith('#') ? text[1..] : text;

        return digits.Length > 0
            && digits.All(char.IsAsciiDigit)
            && int.TryParse(digits, CultureInfo.InvariantCulture, out _);
    }

    [GeneratedRegex(@"\.(rar|7z|zip|r[0-9]{2}|[0-9]{3})$", RegexOptions.IgnoreCase)]
    private static partial Regex ArchiveFileExtensionRegex();

    [GeneratedRegex(@"\.part[0-9]+", RegexOptions.IgnoreCase)]
    private static partial Regex ArchivePartNumberRegex();
}
