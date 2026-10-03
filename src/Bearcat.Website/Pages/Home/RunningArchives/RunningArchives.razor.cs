using Bearcat.Domain.Shared.Transfers;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.RunningArchives;

public partial class RunningArchives : ComponentBase
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

    private void ToggleShowDetails(int archiveId)
    {
        if (!showDetailIds.Remove(archiveId))
        {
            showDetailIds.Add(archiveId);
        }
    }
}
