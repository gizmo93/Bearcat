using Bearcat.Website.Layout.Navigation;

namespace Bearcat.Website.Layout;

public sealed class NavMenuState
{
    private readonly Dictionary<string, bool> openStateById = [];

    public NavMenuState()
    {
        foreach (var submenu in NavigationRegistry.Submenus)
        {
            openStateById[submenu.Id] = false;
        }

        foreach (var section in NavigationRegistry.CollapsibleSections)
        {
            openStateById[section.Id] = true;
        }

        OpenStateIds = openStateById.Keys.ToList();
    }

    public bool Initialized { get; set; }

    public IReadOnlyList<string> OpenStateIds { get; }

    public bool IsOpen(string id) => openStateById[id];

    public void SetOpen(string id, bool open) => openStateById[id] = open;
}
