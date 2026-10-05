using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;
using Microsoft.AspNetCore.Components;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseQuickLookSheet(TimeProvider timeProvider) : ComponentBase
{
    private const string StatusDotBaseClass = "h-2 w-2 shrink-0 rounded-full";

    [Parameter]
    public ReleaseSearchResultReadModel? Release { get; set; }

    [Parameter]
    public bool Open { get; set; }

    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    [Parameter]
    public EventCallback<ReleaseSearchResultReadModel> OnEdit { get; set; }

    private string? SubtitleText =>
        Release switch
        {
            { MetadataTitle: { } title, Year: { } year } => $"{title} · {year}",
            { MetadataTitle: { } title } => title,
            { Year: { } year } => year.ToString(CultureInfo.CurrentCulture),
            _ => null,
        };

    private bool HasTags =>
        Release
            is { Resolution: not null }
                or { Source: not null }
                or { ReleaseType: ReleaseType.Unmanaged }
                or { IsReadyForPostQueue: true };

    private string ContentTypeIconName =>
        ReleaseContentTypeIcons.GetIconName(Release!.ReleaseContentType);

    private string LanguageText =>
        Release!.PrimaryLanguageCode is { } primaryLanguageCode
            ? primaryLanguageCode.ToUpperInvariant()
            : L["NotSet"];

    private static OnlineState GetOnlineState(
        ReleaseSearchResultUploadConfigReadModel uploadConfig
    ) => uploadConfig.IsOnline ? OnlineState.Online : OnlineState.Offline;

    private static string GetStatusDotClass(
        ReleaseSearchResultUploadConfigReadModel uploadConfig
    ) =>
        uploadConfig.IsOnline
            ? $"{StatusDotBaseClass} bg-muted-foreground/55"
            : $"{StatusDotBaseClass} bg-destructive";

    private static string GetStateTextClass(
        ReleaseSearchResultUploadConfigReadModel uploadConfig
    ) =>
        uploadConfig.IsOnline
            ? "shrink-0 text-xs text-muted-foreground"
            : "shrink-0 text-xs font-medium text-destructive";

    private async Task EditAsync()
    {
        var release = Release!;
        await OpenChanged.InvokeAsync(false);
        await OnEdit.InvokeAsync(release);
    }
}
