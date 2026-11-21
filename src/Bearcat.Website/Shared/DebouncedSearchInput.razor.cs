using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public sealed partial class DebouncedSearchInput : ComponentBase, IDisposable
{
    private static readonly TimeSpan SearchTextApplyDelayDuration = TimeSpan.FromMilliseconds(300);

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public EventCallback<string> OnSearchTextChanged { get; set; }

    private readonly RestartableDelay searchTextApplyDelay = new();

    public void Dispose()
    {
        searchTextApplyDelay.Dispose();
    }

    protected override bool ShouldRender()
    {
        return false;
    }

    private async Task ChangeSearchTextAsync(ChangeEventArgs changeEvent)
    {
        var searchText = changeEvent.Value as string ?? string.Empty;

        if (await searchTextApplyDelay.WaitAsync(SearchTextApplyDelayDuration))
        {
            await OnSearchTextChanged.InvokeAsync(searchText);
        }
    }
}
