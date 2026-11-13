using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.Pages.ManageReleases.Results;
using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.ManageReleaseCollections.Results;

public partial class ReleaseCollectionSearchResultRow(TimeProvider timeProvider) : ComponentBase
{
    private const string RowClass =
        $"bearcat-config-row bearcat-release-result-row {ReleaseCollectionSearchResultList.GridColumnsClass}";

    [Parameter]
    [EditorRequired]
    public ReleaseCollectionReadModel ReleaseCollection { get; set; } = null!;

    [Parameter]
    public string? SearchTerm { get; set; }

    [Parameter]
    public bool IsSelected { get; set; }

    [Parameter]
    public EventCallback<bool> OnSelectionToggled { get; set; }

    private string DetailUrl => $"/release-collections/{ReleaseCollection.ReleaseCollectionId}";

    private string OnlineStateDotClass =>
        OnlineStateDotClasses.GetDotClass(ReleaseCollection.OnlineState);

    private string OnlineStateText =>
        ReleaseCollection.OnlineState == OnlineState.Unknown
            ? L["NoUploads"]
            : L.Localize(ReleaseCollection.OnlineState);

    private string ReleaseCountText =>
        ReleaseCollection.ReleaseCount == 1
            ? L["ReleaseCollectionReleaseCountOne"]
            : L["ReleaseCollectionReleaseCount", ReleaseCollection.ReleaseCount];

    private string LanguageCellClass =>
        ReleaseCollection.PrimaryLanguageCode is null ? "min-w-0 max-xl:hidden" : "min-w-0";

    private string ContentTypeIconName =>
        ReleaseContentTypeIcons.GetIconName(ReleaseCollection.ReleaseContentType);

    private Task ToggleSelectionAsync(MouseEventArgs args) =>
        OnSelectionToggled.InvokeAsync(args.ShiftKey);
}
