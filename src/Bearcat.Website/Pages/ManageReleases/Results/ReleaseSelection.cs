namespace Bearcat.Website.Pages.ManageReleases.Results;

public class ReleaseSelection
{
    private readonly HashSet<int> selectedReleaseIds = [];
    private int? lastToggledReleaseId;

    public int Count => selectedReleaseIds.Count;

    public IReadOnlyList<int> SelectedReleaseIds => selectedReleaseIds.ToList();

    public bool IsSelected(int releaseId) => selectedReleaseIds.Contains(releaseId);

    public bool AreAllSelected(IReadOnlyList<int> releaseIds) =>
        releaseIds.Count > 0 && releaseIds.All(selectedReleaseIds.Contains);

    public bool AreSomeButNotAllSelected(IReadOnlyList<int> releaseIds) =>
        releaseIds.Any(selectedReleaseIds.Contains) && !AreAllSelected(releaseIds);

    public void Toggle(IReadOnlyList<int> visibleReleaseIds, int releaseId, bool extendRange)
    {
        var select = !IsSelected(releaseId);
        var releaseIds = visibleReleaseIds.ToList();
        var toggledIndex = releaseIds.IndexOf(releaseId);
        var lastToggledIndex = lastToggledReleaseId is { } lastReleaseId
            ? releaseIds.IndexOf(lastReleaseId)
            : -1;

        if (extendRange && lastToggledIndex >= 0)
        {
            var firstIndex = Math.Min(toggledIndex, lastToggledIndex);
            var lastIndex = Math.Max(toggledIndex, lastToggledIndex);

            for (var index = firstIndex; index <= lastIndex; index++)
            {
                SetSelected(releaseIds[index], select);
            }
        }
        else
        {
            SetSelected(releaseId, select);
        }

        lastToggledReleaseId = releaseId;
    }

    public void SelectAll(IReadOnlyList<int> releaseIds)
    {
        selectedReleaseIds.UnionWith(releaseIds);
    }

    public void Clear()
    {
        selectedReleaseIds.Clear();
        lastToggledReleaseId = null;
    }

    public void KeepOnly(IReadOnlyList<int> releaseIds)
    {
        selectedReleaseIds.IntersectWith(releaseIds);

        if (lastToggledReleaseId is { } lastReleaseId && !releaseIds.Contains(lastReleaseId))
        {
            lastToggledReleaseId = null;
        }
    }

    private void SetSelected(int releaseId, bool select)
    {
        if (select)
        {
            selectedReleaseIds.Add(releaseId);
            return;
        }

        selectedReleaseIds.Remove(releaseId);
    }
}
