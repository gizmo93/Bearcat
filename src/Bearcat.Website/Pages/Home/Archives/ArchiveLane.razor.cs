using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Bearcat.Website.Pages.Home.Activity;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Archives;

public partial class ArchiveLane(
    DialogService dialogService,
    ToastService toastService,
    ITransferCancellationRegistry transferCancellationRegistry
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<RunningArchiveReadModel> Archives { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyDictionary<int, TransferProgressSnapshot> ArchiveProgress { get; set; } = null!;

    private IReadOnlyList<TransferPhase> ArchiveCreationPhases =>
        [
            new(TransferType.ArchiveCreation, L["ArchiveCreationPhasePack"]),
            new(TransferType.ArchiveHashing, L["ArchiveCreationPhaseMd5Hashes"]),
        ];

    private async Task CancelArchiveCreationAsync(RunningArchiveReadModel archive)
    {
        var result = await dialogService.ConfirmAsync(
            L["CancelArchiveCreation"],
            L["CancelArchiveCreationConfirmation", archive.ArchiveId],
            new ConfirmDialogOptions
            {
                ConfirmText = L["CancelArchiveCreation"],
                CancelText = L["Close"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        if (
            transferCancellationRegistry.RequestCancellation(
                new TransferIdentifier(TransferType.ArchiveCreation, archive.ArchiveId)
            )
        )
        {
            toastService.Success(L["ArchiveCreationCancellationRequested", archive.ArchiveId]);
            return;
        }

        toastService.Error(L["ArchiveCreationAlreadyFinished", archive.ArchiveId]);
    }
}
