using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Bearcat.Website.Pages.ManageReleases.Search;

public sealed partial class ReleaseSearchBar : ComponentBase, IDisposable
{
    private const string ScopeListId = "release-search-scope-list";

    private static readonly TimeSpan SearchTermApplyDelay = TimeSpan.FromMilliseconds(300);

    private readonly RestartableDelay searchTermApplyDelay = new();
    private string inputText = string.Empty;
    private string? appliedSearchTerm;
    private bool isFocused;
    private bool isScopeListDismissed;
    private int highlightedEntryIndex;

    [Parameter]
    [EditorRequired]
    public ReleaseSearchQuery Query { get; set; } = new();

    [Parameter]
    public EventCallback<ReleaseSearchQuery> OnQueryChanged { get; set; }

    [Parameter]
    public EventCallback<ReleaseSearchQuery> OnSearchTermTyped { get; set; }

    private bool IsScopeListOpen =>
        isFocused && !isScopeListDismissed && !string.IsNullOrWhiteSpace(inputText);

    private IReadOnlyList<ScopeChip> Chips =>
        Enum.GetValues<ReleaseSearchScope>()
            .Select(scope => new ScopeChip(scope, GetScopeValue(Query, scope)))
            .Where(chip => chip.Value is not null)
            .ToList();

    private IReadOnlyList<ScopeEntry> ScopeEntries
    {
        get
        {
            var matchingScopes = ReleaseSearchScopeDetector.GetMatchingScopes(inputText);

            return
            [
                .. matchingScopes.Select(scope => new ScopeEntry(scope, LooksLikeMatch: true)),
                new ScopeEntry(null, LooksLikeMatch: false),
                .. Enum.GetValues<ReleaseSearchScope>()
                    .Except(matchingScopes)
                    .Select(scope => new ScopeEntry(scope, LooksLikeMatch: false)),
            ];
        }
    }

    protected override void OnParametersSet()
    {
        if (Query.SearchTerm == appliedSearchTerm)
        {
            return;
        }

        appliedSearchTerm = Query.SearchTerm;
        inputText = Query.SearchTerm ?? string.Empty;
    }

    private void HandleInputChanged()
    {
        isScopeListDismissed = false;
        highlightedEntryIndex = 0;

        if (ReleaseSearchScopeDetector.GetMatchingScopes(inputText).Count > 0)
        {
            searchTermApplyDelay.Cancel();
            return;
        }

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

    private async Task ApplySearchTermAsync(EventCallback<ReleaseSearchQuery> queryChanged)
    {
        searchTermApplyDelay.Cancel();

        var searchTerm = SearchUrlParameters.NormalizeText(inputText);
        if (searchTerm == Query.SearchTerm)
        {
            return;
        }

        appliedSearchTerm = searchTerm;
        await queryChanged.InvokeAsync(Query with { SearchTerm = searchTerm });
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "ArrowDown" when IsScopeListOpen:
                MoveHighlight(1);
                break;
            case "ArrowUp" when IsScopeListOpen:
                MoveHighlight(-1);
                break;
            case "ArrowDown":
                isScopeListDismissed = false;
                break;
            case "Enter" when IsScopeListOpen:
                await PickScopeEntryAsync(ScopeEntries[highlightedEntryIndex]);
                break;
            case "Enter":
                await ApplySearchTermAsync(OnQueryChanged);
                break;
            case "Escape":
                isScopeListDismissed = true;
                break;
            case "Backspace" when inputText.Length == 0 && Chips.Count > 0:
                await RemoveChipAsync(Chips[^1].Scope);
                break;
        }
    }

    private void MoveHighlight(int offset)
    {
        var entryCount = ScopeEntries.Count;
        highlightedEntryIndex = (highlightedEntryIndex + offset + entryCount) % entryCount;
    }

    private void HandleFocus()
    {
        isFocused = true;
        isScopeListDismissed = false;
    }

    private void HandleBlur()
    {
        isFocused = false;
    }

    private async Task PickScopeEntryAsync(ScopeEntry entry)
    {
        if (entry.Scope is not { } scope)
        {
            isScopeListDismissed = true;
            await ApplySearchTermAsync(OnQueryChanged);
            return;
        }

        searchTermApplyDelay.Cancel();

        var scopeValue = inputText.Trim();
        inputText = string.Empty;
        appliedSearchTerm = null;
        highlightedEntryIndex = 0;

        await OnQueryChanged.InvokeAsync(
            WithScopeValue(Query with { SearchTerm = null }, scope, scopeValue)
        );
    }

    private Task RemoveChipAsync(ReleaseSearchScope scope) =>
        OnQueryChanged.InvokeAsync(WithScopeValue(Query, scope, null));

    private string GetScopeLabel(ReleaseSearchScope? scope) =>
        scope switch
        {
            null => L["NameOrFolder"],
            ReleaseSearchScope.DownloadLink => L["DownloadLink"],
            ReleaseSearchScope.PostedLocation => L["PostedLocation"],
            ReleaseSearchScope.ArchiveFile => L["ArchiveFile"],
            ReleaseSearchScope.UploadId => L["UploadId"],
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
        };

    private static string GetScopeIconName(ReleaseSearchScope? scope) =>
        scope switch
        {
            null => "folder-search",
            ReleaseSearchScope.DownloadLink => "link",
            ReleaseSearchScope.PostedLocation => "message-square-share",
            ReleaseSearchScope.ArchiveFile => "file-archive",
            ReleaseSearchScope.UploadId => "hash",
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
        };

    private static string GetScopeEntryId(int entryIndex) =>
        $"{ScopeListId}-{entryIndex.ToString(CultureInfo.InvariantCulture)}";

    private static string GetScopeEntryClass(bool isHighlighted) =>
        isHighlighted
            ? "flex cursor-pointer select-none items-center gap-2 rounded-md bg-accent px-2 py-1.5 text-sm text-accent-foreground"
            : "flex cursor-pointer select-none items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-accent/50";

    private static string? GetScopeValue(ReleaseSearchQuery query, ReleaseSearchScope scope) =>
        scope switch
        {
            ReleaseSearchScope.DownloadLink => query.DownloadLink,
            ReleaseSearchScope.PostedLocation => query.PostedLocationUrl,
            ReleaseSearchScope.ArchiveFile => query.ArchiveFileName,
            ReleaseSearchScope.UploadId => query.UploadId,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
        };

    private static ReleaseSearchQuery WithScopeValue(
        ReleaseSearchQuery query,
        ReleaseSearchScope scope,
        string? value
    ) =>
        scope switch
        {
            ReleaseSearchScope.DownloadLink => query with { DownloadLink = value },
            ReleaseSearchScope.PostedLocation => query with { PostedLocationUrl = value },
            ReleaseSearchScope.ArchiveFile => query with { ArchiveFileName = value },
            ReleaseSearchScope.UploadId => query with { UploadId = value },
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
        };

    public void Dispose()
    {
        searchTermApplyDelay.Dispose();
    }

    private sealed record ScopeChip(ReleaseSearchScope Scope, string? Value);

    private sealed record ScopeEntry(ReleaseSearchScope? Scope, bool LooksLikeMatch);
}
