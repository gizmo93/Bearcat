using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.StorageFolderTransfers;

public partial class ArchiveStorageFolderTransferLane(
    DialogService dialogService,
    ToastService toastService,
    ITransferCancellationRegistry transferCancellationRegistry
) : ComponentBase
{
    private static readonly ArchiveStorageFolderTransferLaneTexts MoveLaneTexts = new(
        IconName: "folder-input",
        LaneLabelKey: "ArchiveStorageFolderMoves",
        CancelKey: "CancelArchiveStorageFolderMove",
        CancelConfirmationKey: "CancelArchiveStorageFolderMoveConfirmation",
        CancellationRequestedKey: "ArchiveStorageFolderMoveCancellationRequested",
        AlreadyFinishedKey: "ArchiveStorageFolderMoveAlreadyFinished"
    );

    private static readonly ArchiveStorageFolderTransferLaneTexts LocalWorkingCopyLaneTexts = new(
        IconName: "folder-output",
        LaneLabelKey: "ArchiveLocalWorkingCopies",
        CancelKey: "CancelArchiveLocalWorkingCopy",
        CancelConfirmationKey: "CancelArchiveLocalWorkingCopyConfirmation",
        CancellationRequestedKey: "ArchiveLocalWorkingCopyCancellationRequested",
        AlreadyFinishedKey: "ArchiveLocalWorkingCopyAlreadyFinished"
    );

    [Parameter]
    [EditorRequired]
    public TransferType TransferType { get; set; }

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<RunningArchiveReadModel> Archives { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyDictionary<int, TransferProgressSnapshot> Progress { get; set; } = null!;

    private ArchiveStorageFolderTransferLaneTexts LaneTexts =>
        TransferType switch
        {
            TransferType.ArchiveMoveToStorageFolder => MoveLaneTexts,
            TransferType.ArchiveCopyIntoLocalWorkingCopy => LocalWorkingCopyLaneTexts,
            _ => throw new InvalidOperationException(
                $"Transfer type {TransferType} has no archive storage folder lane"
            ),
        };

    private async Task CancelArchiveStorageFolderTransferAsync(RunningArchiveReadModel archive)
    {
        var result = await dialogService.ConfirmAsync(
            L[LaneTexts.CancelKey],
            L[LaneTexts.CancelConfirmationKey, archive.ArchiveId],
            new ConfirmDialogOptions
            {
                ConfirmText = L[LaneTexts.CancelKey],
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
                new TransferIdentifier(TransferType, archive.ArchiveId)
            )
        )
        {
            toastService.Success(L[LaneTexts.CancellationRequestedKey, archive.ArchiveId]);
            return;
        }

        toastService.Error(L[LaneTexts.AlreadyFinishedKey, archive.ArchiveId]);
    }

    private sealed record ArchiveStorageFolderTransferLaneTexts(
        string IconName,
        string LaneLabelKey,
        string CancelKey,
        string CancelConfirmationKey,
        string CancellationRequestedKey,
        string AlreadyFinishedKey
    );
}
