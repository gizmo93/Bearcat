namespace Bearcat.Website.Pages.ManageReleases.Overview;

public record ReleaseOverviewSummary(
    int OnlineHosterCount,
    int HosterCount,
    int CreatedLinkContainerCount,
    int LinkContainerCount,
    DateTime? LatestUploadAt,
    ArchivePasswordSummary ArchivePassword
);
