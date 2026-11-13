using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.Pages.ManageReleases;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleaseCollections.Search;

public partial class ReleaseCollectionSearchFilters : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public ReleaseCollectionSearchQuery Query { get; set; } = new();

    [Parameter]
    public IReadOnlyList<ReleaseGroupReadModel> ReleaseGroups { get; set; } = [];

    [Parameter]
    public EventCallback<ReleaseCollectionSearchQuery> OnQueryChanged { get; set; }

    private ReleaseCollectionSearchQuery EmptyQuery => new(SortOrder: Query.SortOrder);

    private bool HasActiveSearch => Query != EmptyQuery;

    private IReadOnlyList<SelectOption<ReleaseContentType?>> ReleaseContentTypeOptions =>
        Enum.GetValues<ReleaseContentType>()
            .Select(type => new SelectOption<ReleaseContentType?>(type, L.Localize(type)))
            .ToList();

    private IReadOnlyList<SelectOption<string>> PrimaryLanguageOptions =>
        PrimaryLanguageSelectOptions.Create(L["NotSet"]);

    private IReadOnlyList<SelectOption<int?>> ReleaseGroupOptions =>
        ReleaseGroups
            .Select(group => new SelectOption<int?>(group.ReleaseGroupId, group.Name))
            .ToList();

    private Task ApplyAsync(ReleaseCollectionSearchQuery query) =>
        OnQueryChanged.InvokeAsync(query);

    private Task ResetAllAsync() => OnQueryChanged.InvokeAsync(EmptyQuery);
}
