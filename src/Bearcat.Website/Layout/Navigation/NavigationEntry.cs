using Microsoft.AspNetCore.Components.Routing;

namespace Bearcat.Website.Layout.Navigation;

public sealed record NavigationEntry(
    string LabelKey,
    string TooltipKey,
    string Href,
    NavLinkMatch Match,
    string IconName
) : NavigationItem
{
    public bool IsActiveForPath(string baseRelativePath)
    {
        var path = baseRelativePath.Split('?', '#')[0].Trim('/');
        var entryPath = Href.Trim('/');

        if (Match == NavLinkMatch.All || entryPath.Length == 0)
        {
            return string.Equals(path, entryPath, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(path, entryPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(entryPath + "/", StringComparison.OrdinalIgnoreCase);
    }
}
