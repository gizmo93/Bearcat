using Bearcat.Domain.Shared.Transfers;
using Bearcat.Website.Formatting;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Activity;

public partial class ActivityCard : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string IconName { get; set; } = null!;

    [Parameter]
    public bool IsActive { get; set; }

    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = null!;

    [Parameter]
    public string? TitleHref { get; set; }

    [Parameter]
    public string? Subtitle { get; set; }

    [Parameter]
    public RenderFragment? Chips { get; set; }

    [Parameter]
    public RenderFragment? Phases { get; set; }

    [Parameter]
    public TransferProgressSnapshot? Snapshot { get; set; }

    [Parameter]
    public RenderFragment? StateBadge { get; set; }

    [Parameter]
    public RenderFragment? BelowProgress { get; set; }

    [Parameter]
    public RenderFragment? Files { get; set; }

    [Parameter]
    public string? CancelLabel { get; set; }

    [Parameter]
    public bool CanCancel { get; set; } = true;

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private bool isFilesExpanded;

    private string? GetMetricsText(TransferProgressSnapshot snapshot)
    {
        List<string> parts = [];

        if (TransferFormatting.FormatSpeed(snapshot.BytesPerSecond) is { } speed)
        {
            parts.Add(speed);
        }

        if (TransferFormatting.FormatRemainingTime(snapshot) is { } remainingTime)
        {
            parts.Add(L["TransferTimeLeft", remainingTime]);
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private void ToggleFiles()
    {
        isFilesExpanded = !isFilesExpanded;
    }
}
