namespace Bearcat.Website.Pages.ManagePostedLocations;

public record PostedLocationDisplay(string Initial, string Title, string? Subtitle)
{
    public static PostedLocationDisplay FromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            return new PostedLocationDisplay(GetInitial(url), url, null);
        }

        var domain = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;
        var pathAndQuery = uri.PathAndQuery == "/" ? null : uri.PathAndQuery;

        return new PostedLocationDisplay(GetInitial(domain), domain, pathAndQuery);
    }

    private static string GetInitial(string value) => char.ToUpperInvariant(value[0]).ToString();
}
