namespace Bearcat.Website.Shared;

public class SearchResultSelection
{
    private readonly HashSet<int> selectedIds = [];
    private int? lastToggledId;

    public int Count => selectedIds.Count;

    public IReadOnlyList<int> SelectedIds => selectedIds.ToList();

    public bool IsSelected(int id) => selectedIds.Contains(id);

    public bool AreAllSelected(IReadOnlyList<int> ids) =>
        ids.Count > 0 && ids.All(selectedIds.Contains);

    public bool AreSomeButNotAllSelected(IReadOnlyList<int> ids) =>
        ids.Any(selectedIds.Contains) && !AreAllSelected(ids);

    public void Toggle(IReadOnlyList<int> visibleIds, int id, bool extendRange)
    {
        var select = !IsSelected(id);
        var ids = visibleIds.ToList();
        var toggledIndex = ids.IndexOf(id);
        var lastToggledIndex = lastToggledId is { } lastId ? ids.IndexOf(lastId) : -1;

        if (extendRange && lastToggledIndex >= 0)
        {
            var firstIndex = Math.Min(toggledIndex, lastToggledIndex);
            var lastIndex = Math.Max(toggledIndex, lastToggledIndex);

            for (var index = firstIndex; index <= lastIndex; index++)
            {
                SetSelected(ids[index], select);
            }
        }
        else
        {
            SetSelected(id, select);
        }

        lastToggledId = id;
    }

    public void SelectAll(IReadOnlyList<int> ids)
    {
        selectedIds.UnionWith(ids);
    }

    public void Clear()
    {
        selectedIds.Clear();
        lastToggledId = null;
    }

    public void KeepOnly(IReadOnlyList<int> ids)
    {
        selectedIds.IntersectWith(ids);

        if (lastToggledId is { } lastId && !ids.Contains(lastId))
        {
            lastToggledId = null;
        }
    }

    private void SetSelected(int id, bool select)
    {
        if (select)
        {
            selectedIds.Add(id);
            return;
        }

        selectedIds.Remove(id);
    }
}
