namespace Bearcat.Website.Pages.ManageReleases.DetailTabs;

public static class ReleaseDetailTabSelectionResolver
{
    public static ReleaseDetailTabSelection Resolve(
        string? requestedTab,
        string? requestedView,
        int? focusUploadConfigId
    ) =>
        requestedTab switch
        {
            ReleaseDetailTab.ReleaseInfos => SelectWithDefaultView(ReleaseDetailTab.ReleaseInfos),
            ReleaseDetailTab.Archives => SelectWithDefaultView(ReleaseDetailTab.Archives),
            "upload-configs" => SelectWithDefaultView(ReleaseDetailTab.Uploads),
            ReleaseDetailTab.Uploads => new ReleaseDetailTabSelection(
                ReleaseDetailTab.Uploads,
                ResolveUploadsView(requestedView, focusUploadConfigId)
            ),
            ReleaseDetailTab.Images or "image-upload-configs" or "image-uploads" =>
                SelectWithDefaultView(ReleaseDetailTab.Images),
            _ => SelectWithDefaultView(ReleaseDetailTab.Overview),
        };

    private static ReleaseUploadsView ResolveUploadsView(
        string? requestedView,
        int? focusUploadConfigId
    ) =>
        requestedView switch
        {
            "configuration" => ReleaseUploadsView.Configuration,
            "history" => ReleaseUploadsView.History,
            _ when focusUploadConfigId is not null => ReleaseUploadsView.History,
            _ => ReleaseUploadsView.Configuration,
        };

    private static ReleaseDetailTabSelection SelectWithDefaultView(string tab) =>
        new(tab, ReleaseUploadsView.Configuration);
}
