using Bearcat.Website.Pages.ManageApplicationConfigurations.Sections;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Navigation;

public partial class ConfigurationSectionChipRow(IJSRuntime jsRuntime) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<ConfigurationPageSection> Sections { get; set; } = null!;

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
