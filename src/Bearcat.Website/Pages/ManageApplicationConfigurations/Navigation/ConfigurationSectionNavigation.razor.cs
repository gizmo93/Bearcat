using Bearcat.Website.Pages.ManageApplicationConfigurations.Sections;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Navigation;

public partial class ConfigurationSectionNavigation(IJSRuntime jsRuntime) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<ConfigurationPageSection> Sections { get; set; } = null!;

    private IReadOnlyList<IGrouping<ConfigurationCategory, ConfigurationPageSection>> Categories =>
        Sections.GroupBy(section => section.Appearance.Category).ToList();

    private static string GetHref(ConfigurationPageSection section)
    {
        return $"/configurations#{section.AnchorId}";
    }

    private async Task ScrollToSectionAsync(ConfigurationPageSection section)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("bearcat.scrollElementIntoViewById", section.AnchorId);
        }
        catch (JSDisconnectedException) { }
    }
}
