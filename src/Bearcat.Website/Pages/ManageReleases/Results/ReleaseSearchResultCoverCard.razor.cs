using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Website.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseSearchResultCoverCard : ComponentBase
{
    private const string MirrorBadgeBaseClass = "bearcat-release-cover-mirror-badge";

    [Parameter]
    [EditorRequired]
    public ReleaseSearchResultReadModel Release { get; set; } = null!;

    [Parameter]
    public string? SearchTerm { get; set; }

    [Parameter]
    public bool IsSelected { get; set; }

    [Parameter]
    public bool IsSelectionActive { get; set; }

    [Parameter]
    public bool IsKeyboardFocused { get; set; }

    [Parameter]
    public EventCallback<bool> OnSelectionToggled { get; set; }

    [Parameter]
    public EventCallback OnQuickLook { get; set; }

    private ReleaseMirrorOnlineCount MirrorOnlineCount =>
        ReleaseMirrorOnlineCount.From(Release.UploadConfigs);

    private string MirrorBadgeText =>
        MirrorOnlineCount.HasUploads ? MirrorOnlineCount.Text : L["NoUploads"];

    private string MirrorBadgeClass =>
        MirrorOnlineCount.HasUploads && !MirrorOnlineCount.AreAllOnline
            ? $"{MirrorBadgeBaseClass} bearcat-release-cover-mirror-badge-offline"
            : MirrorBadgeBaseClass;

    private string DetailsText
    {
        get
        {
            List<string> parts = [];

            if (Release.Resolution is { } resolution)
            {
                parts.Add(L.Localize(resolution));
            }

            if (Release.Source is { } source)
            {
                parts.Add(L.Localize(source));
            }

            parts.Add(Release.ReleaseGroupName);

            return string.Join(" · ", parts);
        }
    }

    private Task ToggleSelectionAsync(MouseEventArgs args) =>
        OnSelectionToggled.InvokeAsync(args.ShiftKey);
}
