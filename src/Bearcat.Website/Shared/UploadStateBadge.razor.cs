using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class UploadStateBadge : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public UploadState State { get; set; }

    private BadgeVariant Variant =>
        State switch
        {
            UploadState.Uploading => BadgeVariant.Default,
            UploadState.CancellationRequested => BadgeVariant.Secondary,
            UploadState.Pending => BadgeVariant.Secondary,
            UploadState.Failed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };
}
