using Bearcat.Domain.Shared.Transfers;
using Bearcat.Website.Pages.Home.Activity;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.ReleaseFolderExtractions;

public partial class ReleaseFolderExtractionLane : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<TransferProgressSnapshot> Snapshots { get; set; } = null!;

    private IReadOnlyList<TransferPhase> ReleaseFolderPhases =>
        [
            new(TransferType.ReleaseFolderVerification, L["TransferPhaseVerify"]),
            new(TransferType.ReleaseFolderExtraction, L["TransferPhaseExtract"]),
        ];
}
