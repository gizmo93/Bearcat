using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.StorageFolderMoves;

public partial class ArchiveStorageFolderMoveLane(
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
    public IReadOnlyDictionary<int, TransferProgressSnapshot> MoveProgress { get; set; } = null!;

    private async Task CancelArchiveStorageFolderMoveAsync(RunningArchiveReadModel archive)
    {
        var result = await dialogService.ConfirmAsync(
            L["CancelArchiveStorageFolderMove"],
            L["CancelArchiveStorageFolderMoveConfirmation", archive.ArchiveId],
            new ConfirmDialogOptions
            {
                ConfirmText = L["CancelArchiveStorageFolderMove"],
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
                new TransferIdentifier(TransferType.ArchiveMoveToStorageFolder, archive.ArchiveId)
            )
        )
        {
            toastService.Success(
                L["ArchiveStorageFolderMoveCancellationRequested", archive.ArchiveId]
            );
            return;
        }

        toastService.Error(L["ArchiveStorageFolderMoveAlreadyFinished", archive.ArchiveId]);
    }
}
