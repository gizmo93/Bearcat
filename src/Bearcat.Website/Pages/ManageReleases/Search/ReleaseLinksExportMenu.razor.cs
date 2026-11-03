using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Controllers;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.ManageReleases.Search;

public partial class ReleaseLinksExportMenu(TimeProvider timeProvider)
{
    private static string GetDownloadUrl(OnlineState? onlineState)
    {
        return onlineState is null
            ? "/releases/links-export"
            : $"/releases/links-export?onlineState={onlineState}";
    }

    private string GetFileName(OnlineState? onlineState)
    {
        return ReleaseLinksExportController.GetFileName(onlineState, timeProvider.GetLocalNow());
    }
}
