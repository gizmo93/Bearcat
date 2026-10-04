using Bearcat.Website.Pages.ManageReleases.DetailTabs;
using Bearcat.Website.Pages.ManageReleases.ProgressSteps;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleases.ProgressSteps;

public class ReleaseProgressStepTabResolverTest
{
    [TestCase(ReleaseProgressStepKind.Info, ReleaseDetailTab.ReleaseInfos)]
    [TestCase(ReleaseProgressStepKind.Archived, ReleaseDetailTab.Archives)]
    [TestCase(ReleaseProgressStepKind.Uploaded, ReleaseDetailTab.Uploads)]
    [TestCase(ReleaseProgressStepKind.LinkContainers, ReleaseDetailTab.Overview)]
    [TestCase(ReleaseProgressStepKind.Posted, ReleaseDetailTab.Overview)]
    public void GetTab_ReturnsTabShowingTheStepDetails(
        ReleaseProgressStepKind kind,
        string expectedTab
    )
    {
        // Act
        var tab = ReleaseProgressStepTabResolver.GetTab(kind);

        // Assert
        tab.ShouldBe(expectedTab);
    }
}
