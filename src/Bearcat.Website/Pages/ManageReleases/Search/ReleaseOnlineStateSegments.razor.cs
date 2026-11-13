using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Search;

public partial class ReleaseOnlineStateSegments : ComponentBase
{
    private const string SegmentBaseClass =
        "inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-md border px-3 py-1.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring";

    private const string SelectedSegmentClass =
        $"{SegmentBaseClass} border-transparent bg-background text-foreground shadow-sm dark:border-input dark:bg-input/40";

    private const string UnselectedSegmentClass =
        $"{SegmentBaseClass} border-transparent text-muted-foreground hover:text-foreground";

    [Parameter]
    public ReleaseOnlineStateCounts? Counts { get; set; }

    [Parameter]
    public OnlineState? SelectedOnlineState { get; set; }

    [Parameter]
    public EventCallback<OnlineState?> SelectedOnlineStateChanged { get; set; }

    private IReadOnlyList<OnlineStateSegment> Segments =>
        [
            new(null, L["All"], Counts?.TotalCount ?? 0, null),
            new(
                OnlineState.Online,
                L.Localize(OnlineState.Online),
                Counts?.OnlineCount ?? 0,
                OnlineStateDotClasses.GetDotClass(OnlineState.Online)
            ),
            new(
                OnlineState.PartiallyOnline,
                L.Localize(OnlineState.PartiallyOnline),
                Counts?.PartiallyOnlineCount ?? 0,
                OnlineStateDotClasses.GetDotClass(OnlineState.PartiallyOnline)
            ),
            new(
                OnlineState.Offline,
                L.Localize(OnlineState.Offline),
                Counts?.OfflineCount ?? 0,
                OnlineStateDotClasses.GetDotClass(OnlineState.Offline)
            ),
            new(
                OnlineState.Unknown,
                L["NoUploads"],
                Counts?.WithoutUploadConfigsCount ?? 0,
                OnlineStateDotClasses.GetDotClass(OnlineState.Unknown)
            ),
        ];

    private static string GetSegmentClass(bool isSelected) =>
        isSelected ? SelectedSegmentClass : UnselectedSegmentClass;

    private async Task SelectAsync(OnlineState? onlineState)
    {
        if (onlineState == SelectedOnlineState)
        {
            return;
        }

        await SelectedOnlineStateChanged.InvokeAsync(onlineState);
    }

    private sealed record OnlineStateSegment(
        OnlineState? OnlineState,
        string Label,
        int Count,
        string? StateDotClass
    );
}
