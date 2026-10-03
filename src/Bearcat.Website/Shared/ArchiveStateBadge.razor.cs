using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class ArchiveStateBadge : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public ArchiveState State { get; set; }

    private BadgeVariant Variant =>
        State switch
        {
            ArchiveState.Created => BadgeVariant.Default,
            ArchiveState.CreationFailed => BadgeVariant.Destructive,
            ArchiveState.MissingFiles => BadgeVariant.Destructive,
            ArchiveState.Creating => BadgeVariant.Secondary,
            ArchiveState.Restoring => BadgeVariant.Secondary,
            _ => BadgeVariant.Outline,
        };
}
