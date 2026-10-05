using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Summary;

public partial class ActivitySummary : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public double UploadBytesPerSecond { get; set; }

    [Parameter]
    [EditorRequired]
    public double DownloadBytesPerSecond { get; set; }

    [Parameter]
    [EditorRequired]
    public TransferSpeedHistory UploadSpeedHistory { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public TransferSpeedHistory DownloadSpeedHistory { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<RunningArchiveReadModel> RunningArchives { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyDictionary<int, TransferProgressSnapshot> ArchiveProgress { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public int QueuedUploadCount { get; set; }

    private string? ArchiveSubLabel
    {
        get
        {
            if (RunningArchives.Count == 0)
            {
                return null;
            }

            if (
                RunningArchives.Count > 1
                || !ArchiveProgress.TryGetValue(RunningArchives[0].ArchiveId, out var snapshot)
            )
            {
                return L["Running"];
            }

            return snapshot.Identifier.Type switch
            {
                TransferType.ArchiveCreation => L["ArchivePhasePacking"],
                TransferType.ArchiveHashing => L["ArchivePhaseCreatingMd5Hashes"],
                TransferType.ArchiveHashChange => L[
                    "ArchivePhaseChangingMd5HashesOfExistingArchive"
                ],
                _ => throw new ArgumentOutOfRangeException(
                    nameof(snapshot),
                    snapshot.Identifier.Type,
                    "Unexpected transfer type for running archive"
                ),
            };
        }
    }
}
