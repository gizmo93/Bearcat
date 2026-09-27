using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.Transfers;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.RunningArchives;

public partial class RunningArchives : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<Archive> Archives { get; set; } = null!;

    [Parameter]
    public IReadOnlyDictionary<int, TransferProgressSnapshot> ArchiveProgress { get; set; } =
        new Dictionary<int, TransferProgressSnapshot>();

    private readonly HashSet<int> showDetailIds = [];

    private IReadOnlyList<Archive> ExpandedArchives =>
        Archives
            .Where(archive =>
                showDetailIds.Contains(archive.Id) && ArchiveProgress.ContainsKey(archive.Id)
            )
            .ToList();

    private double GetProgress(Archive archive)
    {
        return ArchiveProgress.TryGetValue(archive.Id, out var snapshot) ? snapshot.Percentage : 0;
    }

    private void ToggleShowDetails(int archiveId)
    {
        if (!showDetailIds.Remove(archiveId))
        {
            showDetailIds.Add(archiveId);
        }
    }
}
