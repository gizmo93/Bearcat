using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseSearchResultRow(TimeProvider timeProvider) : ComponentBase
{
    private const string TagBaseClass =
        "inline-flex items-center rounded px-1.5 text-[0.6875rem] font-medium leading-[1.125rem]";

    private const string MutedTagClass = $"{TagBaseClass} bg-muted text-muted-foreground";
    private const string OutlineTagClass =
        $"{TagBaseClass} border border-border text-muted-foreground";
    private const string PrimaryTagClass = $"{TagBaseClass} bg-primary/12 text-primary";

    private const string RowClass =
        $"bearcat-config-row bearcat-release-result-row {ReleaseSearchResultList.GridColumnsClass}";

    [Parameter]
    [EditorRequired]
    public ReleaseSearchResultReadModel Release { get; set; } = null!;

    [Parameter]
    public string? SearchTerm { get; set; }

    [Parameter]
    public bool IsSelected { get; set; }

    [Parameter]
    public EventCallback<bool> OnSelectionToggled { get; set; }

    [Parameter]
    public EventCallback OnEdit { get; set; }

    [Parameter]
    public EventCallback OnDelete { get; set; }

    private string? MetadataTitleText =>
        Release.MetadataTitle switch
        {
            null => null,
            var title when Release.Year is { } year => $"{title} ({year})",
            var title => title,
        };

    private string LanguageCellClass =>
        Release.PrimaryLanguageCode is null ? "min-w-0 max-xl:hidden" : "min-w-0";

    private string ContentTypeIconName =>
        Release.ReleaseContentType switch
        {
            ReleaseContentType.Movie => "film",
            ReleaseContentType.TvShowEpisode => "tv",
            ReleaseContentType.Game => "gamepad-2",
            ReleaseContentType.Other => "file",
            _ => throw new ArgumentOutOfRangeException(
                nameof(Release.ReleaseContentType),
                Release.ReleaseContentType,
                null
            ),
        };

    private Task ToggleSelectionAsync(MouseEventArgs args) =>
        OnSelectionToggled.InvokeAsync(args.ShiftKey);
}
