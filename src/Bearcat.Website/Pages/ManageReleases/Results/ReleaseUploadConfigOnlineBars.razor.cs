using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseUploadConfigOnlineBars : ComponentBase
{
    private const string BarBaseClass = "h-3.5 w-1.5 shrink-0 rounded-sm";
    private const string OnlineBarClass = $"{BarBaseClass} bg-muted-foreground/55";
    private const string PartiallyOnlineBarClass = $"{BarBaseClass} bg-destructive/45";
    private const string OfflineBarClass = $"{BarBaseClass} bg-destructive";

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<ReleaseSearchResultUploadConfigReadModel> UploadConfigs { get; set; } = [];

    private ReleaseMirrorOnlineCount MirrorOnlineCount =>
        ReleaseMirrorOnlineCount.From(UploadConfigs);

    private string CountClass =>
        MirrorOnlineCount.AreAllOnline
            ? "text-xs tabular-nums text-muted-foreground"
            : "text-xs font-medium tabular-nums text-destructive";

    private string AriaLabel => $"{L["Mirrors"]}: {MirrorOnlineCount.Text}";

    private static string GetBarClass(ReleaseSearchResultUploadConfigReadModel uploadConfig) =>
        uploadConfig.OnlineState switch
        {
            OnlineState.Online => OnlineBarClass,
            OnlineState.PartiallyOnline => PartiallyOnlineBarClass,
            _ => OfflineBarClass,
        };

    private static string GetStateTextClass(
        ReleaseSearchResultUploadConfigReadModel uploadConfig
    ) => uploadConfig.OnlineState == OnlineState.Online ? string.Empty : "font-medium";
}
