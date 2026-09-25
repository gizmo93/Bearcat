namespace Bearcat.Website.Layout.Navigation;

public sealed record NavigationSubmenu(
    string Id,
    string LabelKey,
    string IconName,
    IReadOnlyList<NavigationEntry> Entries
) : NavigationItem
{
    public bool ContainsActiveEntryForPath(string baseRelativePath) =>
        Entries.Any(entry => entry.IsActiveForPath(baseRelativePath));
}
