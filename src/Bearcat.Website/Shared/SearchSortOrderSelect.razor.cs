using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class SearchSortOrderSelect<TSortOrder> : ComponentBase
    where TSortOrder : struct, Enum
{
    [Parameter]
    public TSortOrder Value { get; set; }

    [Parameter]
    public EventCallback<TSortOrder> ValueChanged { get; set; }

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<SelectOption<TSortOrder>> Options { get; set; } = [];

    private async Task SelectAsync(TSortOrder sortOrder)
    {
        if (EqualityComparer<TSortOrder>.Default.Equals(sortOrder, Value))
        {
            return;
        }

        await ValueChanged.InvokeAsync(sortOrder);
    }
}
