using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class LinkContainerChip : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public string LinkCrypterRegistrationName { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public LinkCrypterContainerState State { get; set; }

    [Parameter]
    [EditorRequired]
    public string ContainerUrl { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<string> Errors { get; set; } = [];

    [Parameter]
    public bool IsCollectionContainer { get; set; }

    [Parameter]
    public string? CoverageText { get; set; }

    [Parameter]
    public string? CoverageTitle { get; set; }

    [Parameter]
    public EventCallback OnDeleteRequested { get; set; }

    private bool IsCreated => State is LinkCrypterContainerState.Created;

    private bool HasUrl => !string.IsNullOrWhiteSpace(ContainerUrl);

    private bool IsDeleteButtonShown =>
        State is LinkCrypterContainerState.CreationFailed && OnDeleteRequested.HasDelegate;

    private BadgeVariant StateBadgeVariant =>
        State switch
        {
            LinkCrypterContainerState.Created => BadgeVariant.Secondary,
            LinkCrypterContainerState.CreationFailed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };
}
