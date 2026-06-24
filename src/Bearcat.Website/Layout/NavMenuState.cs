namespace Bearcat.Website.Layout;

public sealed class NavMenuState
{
    public bool Initialized { get; set; }

    public bool ConfigurationOpen { get; set; } = true;

    public bool RegistrationsOpen { get; set; } = true;
}
