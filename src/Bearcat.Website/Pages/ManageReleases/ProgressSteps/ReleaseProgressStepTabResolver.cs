using Bearcat.Website.Pages.ManageReleases.DetailTabs;

namespace Bearcat.Website.Pages.ManageReleases.ProgressSteps;

public static class ReleaseProgressStepTabResolver
{
    public static string GetTab(ReleaseProgressStepKind kind) =>
        kind switch
        {
            ReleaseProgressStepKind.Info => ReleaseDetailTab.ReleaseInfos,
            ReleaseProgressStepKind.Archived => ReleaseDetailTab.Archives,
            ReleaseProgressStepKind.Uploaded => ReleaseDetailTab.Uploads,
            ReleaseProgressStepKind.LinkContainers => ReleaseDetailTab.Overview,
            ReleaseProgressStepKind.Posted => ReleaseDetailTab.Overview,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
}
