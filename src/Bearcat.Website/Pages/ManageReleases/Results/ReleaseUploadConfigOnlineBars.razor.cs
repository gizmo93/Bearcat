using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseUploadConfigOnlineBars : ComponentBase
{
    private const string BarBaseClass = "h-3.5 w-1.5 shrink-0 rounded-sm";
    private const string OnlineBarClass = $"{BarBaseClass} bg-muted-foreground/55";
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
        uploadConfig.IsOnline ? OnlineBarClass : OfflineBarClass;
}
