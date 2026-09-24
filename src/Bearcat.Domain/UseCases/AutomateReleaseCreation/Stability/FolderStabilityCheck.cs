using Bearcat.Abstractions;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.Stability;

public static class FolderStabilityCheck
{
    public static FolderStability GetStability(
        FolderFileCountAndSize observed,
        FolderFileCountAndSize current,
        DateTime lastChangedAt,
        DateTime now,
        TimeSpan stabilityWindow
    )
    {
        if (observed != current)
        {
            return FolderStability.Changed;
        }

        return now - lastChangedAt < stabilityWindow
            ? FolderStability.NotYetStable
            : FolderStability.Stable;
    }
}
