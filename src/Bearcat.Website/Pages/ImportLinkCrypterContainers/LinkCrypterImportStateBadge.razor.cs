using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ImportLinkCrypterContainers;

public partial class LinkCrypterImportStateBadge : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public LinkCrypterImportState State { get; set; }

    private BadgeVariant Variant =>
        State switch
        {
            LinkCrypterImportState.Failed => BadgeVariant.Destructive,
            LinkCrypterImportState.ReadingContainers
            or LinkCrypterImportState.CheckingFiles
            or LinkCrypterImportState.Importing => BadgeVariant.Default,
            LinkCrypterImportState.Completed => BadgeVariant.Secondary,
            _ => BadgeVariant.Outline,
        };
}
