using Bearcat.Website.Pages.ManageReleases.Results;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Search;

public partial class ReleaseSearchResultViewToggle : ComponentBase
{
    [Parameter]
    public ReleaseSearchResultView Value { get; set; }

    [Parameter]
    public EventCallback<ReleaseSearchResultView> ValueChanged { get; set; }

    private async Task SelectAsync(ReleaseSearchResultView view)
    {
        if (view == Value)
        {
            return;
        }

        await ValueChanged.InvokeAsync(view);
    }
}
