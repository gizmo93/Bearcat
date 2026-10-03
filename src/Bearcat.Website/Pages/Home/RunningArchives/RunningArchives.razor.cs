using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.RunningArchives;

public partial class RunningArchives(
    DialogService dialogService,
    ToastService toastService,
    ITransferCancellationRegistry transferCancellationRegistry
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<RunningArchiveReadModel> Archives { get; set; } = null!;

    [Parameter]
    public IReadOnlyDictionary<int, TransferProgressSnapshot> ArchiveProgress { get; set; } =
        new Dictionary<int, TransferProgressSnapshot>();

    private readonly HashSet<int> showDetailIds = [];

    private IReadOnlyList<RunningArchiveReadModel> ExpandedArchives =>
        Archives
            .Where(archive =>
                showDetailIds.Contains(archive.ArchiveId)
                && ArchiveProgress.ContainsKey(archive.ArchiveId)
            )
            .ToList();

    private double GetProgress(RunningArchiveReadModel archive)
    {
        return ArchiveProgress.TryGetValue(archive.ArchiveId, out var snapshot)
            ? snapshot.Percentage
            : 0;
    }

    private string GetPhaseLabel(TransferProgressSnapshot snapshot)
    {
        return snapshot.Identifier.Type switch
        {
            TransferType.ArchiveCreation => L["ArchivePhasePacking"],
            TransferType.ArchiveHashing => L["ArchivePhaseCreatingMd5Hashes"],
            TransferType.ArchiveHashChange => L["ArchivePhaseChangingMd5HashesOfExistingArchive"],
            _ => throw new ArgumentOutOfRangeException(
                nameof(snapshot),
                $"Unexpected transfer type for running archive, {snapshot.Identifier.Type}"
            ),
        };
    }

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

    private void ToggleShowDetails(int archiveId)
    {
        if (!showDetailIds.Remove(archiveId))
        {
            showDetailIds.Add(archiveId);
        }
    }
}
