using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseBulkActionMenuButton<TValue> : ComponentBase
{
    private readonly string searchInputId = $"release-bulk-action-search-{Guid.NewGuid():N}";
    private bool isOpen;
    private bool openedByTriggerClick;

    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public string Icon { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<SelectOption<TValue>> Options { get; set; } = [];

    [Parameter]
    public EventCallback<TValue> OnSelect { get; set; }

    private static bool MatchesSearchQuery(CommandItemMetadata item, string searchQuery) =>
        (item.SearchText ?? string.Empty).Contains(
            searchQuery,
            StringComparison.CurrentCultureIgnoreCase
        );

    private void HandleTriggerClick()
    {
        if (openedByTriggerClick)
        {
            openedByTriggerClick = false;
            return;
        }

        isOpen = false;
    }

    private void HandleOpenChanged(bool open)
    {
        isOpen = open;
        openedByTriggerClick = open;
    }

    private async Task SelectAsync(TValue value)
    {
        isOpen = false;
        openedByTriggerClick = false;
        await OnSelect.InvokeAsync(value);
    }
}
