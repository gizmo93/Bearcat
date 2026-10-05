using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Search;

public partial class ReleaseSearchFilterButton<TValue> : ComponentBase
{
    private const string ClearFilterItemValue = "clear-filter";

    private readonly string searchInputId = $"release-filter-search-{Guid.NewGuid():N}";
    private bool isOpen;
    private bool openedByTriggerClick;

    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<SelectOption<TValue>> Options { get; set; } = [];

    [Parameter]
    public TValue? Value { get; set; }

    [Parameter]
    public EventCallback<TValue?> ValueChanged { get; set; }

    [Parameter]
    public bool ShowSearch { get; set; }

    private bool IsActive => Value is not null;

    private string TriggerClass =>
        IsActive
            ? "bearcat-release-filter-button bearcat-release-filter-button-active"
            : "bearcat-release-filter-button";

    private string SelectedOptionText =>
        Options.FirstOrDefault(option => IsSelected(option.Value))?.Text
        ?? Value?.ToString()
        ?? string.Empty;

    private bool IsSelected(TValue optionValue) =>
        EqualityComparer<TValue>.Default.Equals(optionValue, Value);

    private string GetCheckIconClass(TValue optionValue) =>
        IsSelected(optionValue) ? "h-4 w-4" : "h-4 w-4 opacity-0";

    private static bool MatchesSearchQuery(CommandItemMetadata item, string searchQuery) =>
        item.Value == ClearFilterItemValue
        || (item.SearchText ?? string.Empty).Contains(
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

    private async Task SelectAsync(TValue optionValue)
    {
        Close();

        if (IsSelected(optionValue))
        {
            return;
        }

        await ValueChanged.InvokeAsync(optionValue);
    }

    private async Task ClearAsync()
    {
        Close();
        await ValueChanged.InvokeAsync(default);
    }

    private void Close()
    {
        isOpen = false;
        openedByTriggerClick = false;
    }
}
