using Bearcat.Website.Pages.ManageReleases.DetailTabs;
using Shouldly;

namespace Bearcat.Website.UnitTest.Pages.ManageReleases.DetailTabs;

public class ReleaseDetailTabSelectionResolverTest
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("unknown")]
    [TestCase("overview")]
    public void Resolve_MissingUnknownOrOverviewTab_ReturnsOverview(string? requestedTab)
    {
        // Act
        var selection = ReleaseDetailTabSelectionResolver.Resolve(requestedTab, null, null);

        // Assert
        selection.ShouldBe(
            new ReleaseDetailTabSelection(
                ReleaseDetailTab.Overview,
                ReleaseUploadsView.Configuration
            )
        );
    }

    [TestCase("release-infos", ReleaseDetailTab.ReleaseInfos)]
    [TestCase("archives", ReleaseDetailTab.Archives)]
    [TestCase("images", ReleaseDetailTab.Images)]
    [TestCase("image-upload-configs", ReleaseDetailTab.Images)]
    [TestCase("image-uploads", ReleaseDetailTab.Images)]
    public void Resolve_NonUploadsTab_ReturnsMatchingTabWithConfigurationView(
        string requestedTab,
        string expectedTab
    )
    {
        // Act
        var selection = ReleaseDetailTabSelectionResolver.Resolve(requestedTab, null, 7);

        // Assert
        selection.ShouldBe(
            new ReleaseDetailTabSelection(expectedTab, ReleaseUploadsView.Configuration)
        );
    }

    [TestCase(null, null)]
    [TestCase(null, 7)]
    [TestCase("history", 7)]
    public void Resolve_LegacyUploadConfigsTab_ReturnsUploadsTabWithConfigurationView(
        string? requestedView,
        int? focusUploadConfigId
    )
    {
        // Act
        var selection = ReleaseDetailTabSelectionResolver.Resolve(
            "upload-configs",
            requestedView,
            focusUploadConfigId
        );

        // Assert
        selection.ShouldBe(
            new ReleaseDetailTabSelection(
                ReleaseDetailTab.Uploads,
                ReleaseUploadsView.Configuration
            )
        );
    }

    [TestCase("configuration", null, ReleaseUploadsView.Configuration)]
    [TestCase("configuration", 7, ReleaseUploadsView.Configuration)]
    [TestCase("history", null, ReleaseUploadsView.History)]
    [TestCase("history", 7, ReleaseUploadsView.History)]
    public void Resolve_UploadsTabWithView_ReturnsRequestedView(
        string requestedView,
        int? focusUploadConfigId,
        ReleaseUploadsView expectedView
    )
    {
        // Act
        var selection = ReleaseDetailTabSelectionResolver.Resolve(
            "uploads",
            requestedView,
            focusUploadConfigId
        );

        // Assert
        selection.ShouldBe(new ReleaseDetailTabSelection(ReleaseDetailTab.Uploads, expectedView));
    }

    [Test]
    public void Resolve_UploadsTabWithoutViewAndWithUploadConfigId_ReturnsHistoryView()
    {
        // Act
        var selection = ReleaseDetailTabSelectionResolver.Resolve("uploads", null, 7);

        // Assert
        selection.ShouldBe(
            new ReleaseDetailTabSelection(ReleaseDetailTab.Uploads, ReleaseUploadsView.History)
        );
    }

    [TestCase(null)]
    [TestCase("unknown")]
    public void Resolve_UploadsTabWithoutValidViewAndWithoutUploadConfigId_ReturnsConfigurationView(
        string? requestedView
    )
    {
        // Act
        var selection = ReleaseDetailTabSelectionResolver.Resolve("uploads", requestedView, null);

        // Assert
        selection.ShouldBe(
            new ReleaseDetailTabSelection(
                ReleaseDetailTab.Uploads,
                ReleaseUploadsView.Configuration
            )
        );
    }
}
