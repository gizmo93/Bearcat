namespace Bearcat.Hosters.Shared;

public static class FileLinkHostMatching
{
    public static bool HostMatchesAnyHostName(string host, IReadOnlyList<string> hostNames)
    {
        return hostNames.Any(hostName =>
            string.Equals(host, hostName, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith($".{hostName}", StringComparison.OrdinalIgnoreCase)
        );
    }

    public static Uri? ParseFileLinkWithAnyHostName(
        string fileLink,
        IReadOnlyList<string> hostNames
    )
    {
        if (
            !Uri.TryCreate(fileLink.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
        )
        {
            return null;
        }

        return HostMatchesAnyHostName(uri.Host, hostNames) ? uri : null;
    }

    public static List<string> GetPathSegments(Uri fileLinkUri)
    {
        return fileLinkUri
            .AbsolutePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .ToList();
    }
}
