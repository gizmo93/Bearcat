using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Bearcat.Website.Pages.ManageReleaseCollections.Search;

public sealed partial class ReleaseCollectionSearchBar : ComponentBase, IDisposable
{
    private static readonly TimeSpan SearchTermApplyDelay = TimeSpan.FromMilliseconds(300);

    private readonly RestartableDelay searchTermApplyDelay = new();
    private readonly List<string?> appliedSearchTermsNotYetReceived = [];
    private string inputText = string.Empty;
    private string? appliedSearchTerm;
    private string? lastReceivedSearchTerm;
    private bool isFocused;

    [Parameter]
    public string? SearchTerm { get; set; }

    [Parameter]
    public EventCallback<string?> OnSearchTermChanged { get; set; }

    [Parameter]
    public EventCallback<string?> OnSearchTermTyped { get; set; }

    protected override void OnParametersSet()
    {
        if (SearchTerm == lastReceivedSearchTerm)
        {
            return;
        }

        lastReceivedSearchTerm = SearchTerm;

        var indexOfReceivedSearchTerm = appliedSearchTermsNotYetReceived.IndexOf(SearchTerm);

        if (indexOfReceivedSearchTerm >= 0)
        {
            appliedSearchTermsNotYetReceived.RemoveRange(0, indexOfReceivedSearchTerm + 1);
            return;
        }

        appliedSearchTermsNotYetReceived.Clear();
        appliedSearchTerm = SearchTerm;
        inputText = SearchTerm ?? string.Empty;
    }

    private void HandleInputChanged()
    {
        _ = ApplySearchTermAfterDelayAsync();
    }

    private async Task ApplySearchTermAfterDelayAsync()
    {
        if (!await searchTermApplyDelay.WaitAsync(SearchTermApplyDelay))
        {
            return;
        }

        await InvokeAsync(() => ApplySearchTermAsync(OnSearchTermTyped));
    }

    private async Task ApplySearchTermAsync(EventCallback<string?> searchTermChanged)
    {
        searchTermApplyDelay.Cancel();

        var searchTerm = SearchUrlParameters.NormalizeText(inputText);

        if (searchTerm == appliedSearchTerm)
        {
            return;
        }

        appliedSearchTerm = searchTerm;
        appliedSearchTermsNotYetReceived.Add(searchTerm);
        await searchTermChanged.InvokeAsync(searchTerm);
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await ApplySearchTermAsync(OnSearchTermChanged);
        }
    }

    private void HandleFocus()
    {
        isFocused = true;
    }

    private void HandleBlur()
    {
        isFocused = false;
    }

    public void Dispose()
    {
        searchTermApplyDelay.Dispose();
    }
}
