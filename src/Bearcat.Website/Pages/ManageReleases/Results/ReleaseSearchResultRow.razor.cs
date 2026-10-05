using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public partial class ReleaseSearchResultRow(TimeProvider timeProvider) : ComponentBase
{
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
    public bool IsKeyboardFocused { get; set; }

    [Parameter]
    public EventCallback<bool> OnSelectionToggled { get; set; }

    [Parameter]
    public EventCallback OnQuickLook { get; set; }

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
        ReleaseContentTypeIcons.GetIconName(Release.ReleaseContentType);

    private Task ToggleSelectionAsync(MouseEventArgs args) =>
        OnSelectionToggled.InvokeAsync(args.ShiftKey);
}
