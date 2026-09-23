using Bearcat.Abstractions;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.Stability;

public static class FolderStabilityGate
{
    public static FolderStability Evaluate(
        FolderContentFingerprint observed,
        FolderContentFingerprint current,
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
            ? FolderStability.Settling
            : FolderStability.Stable;
    }
}
