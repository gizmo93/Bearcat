namespace Bearcat.Website.Pages.ManageReleases.Results;

public class ReleaseKeyboardFocus
{
    public int? FocusedReleaseId { get; private set; }

    public void MoveToNext(IReadOnlyList<int> releaseIds) => Move(releaseIds, step: 1);

    public void MoveToPrevious(IReadOnlyList<int> releaseIds) => Move(releaseIds, step: -1);

    public void Focus(int releaseId)
    {
        FocusedReleaseId = releaseId;
    }

    public void Clear()
    {
        FocusedReleaseId = null;
    }

    public void KeepOnly(IReadOnlyList<int> releaseIds)
    {
        if (FocusedReleaseId is { } releaseId && !releaseIds.Contains(releaseId))
        {
            FocusedReleaseId = null;
        }
    }

    private void Move(IReadOnlyList<int> releaseIds, int step)
    {
        if (releaseIds.Count == 0)
        {
            return;
        }

        var focusedIndex = FocusedReleaseId is { } releaseId
            ? releaseIds.ToList().IndexOf(releaseId)
            : -1;
        var targetIndex =
            focusedIndex < 0 ? 0 : Math.Clamp(focusedIndex + step, 0, releaseIds.Count - 1);

        FocusedReleaseId = releaseIds[targetIndex];
    }
}
