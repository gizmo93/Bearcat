using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class RemoteSourceDownloadStateBadge : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public RemoteSourceDownloadState State { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private BadgeVariant Variant =>
        State switch
        {
            RemoteSourceDownloadState.Failed => BadgeVariant.Destructive,
            RemoteSourceDownloadState.ReleaseCreated => BadgeVariant.Default,
            RemoteSourceDownloadState.Canceled or RemoteSourceDownloadState.Ignored =>
                BadgeVariant.Outline,
            _ => BadgeVariant.Secondary,
        };
}
