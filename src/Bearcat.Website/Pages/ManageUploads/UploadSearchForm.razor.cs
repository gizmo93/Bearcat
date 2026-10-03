using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageUploads;

public partial class UploadSearchForm : ComponentBase
{
    [Parameter]
    public IReadOnlyList<HosterRegistrationReadModel> HosterRegistrations { get; set; } = [];

    [Parameter]
    public IReadOnlyList<ReleaseGroupReadModel> ReleaseGroups { get; set; } = [];

    [Parameter]
    public UploadSearchQuery Query { get; set; } = new();

    [Parameter]
    public EventCallback<UploadSearchQuery> OnSearch { get; set; }

    private UploadSearchFormModel model = new();
    private UploadSearchQuery? syncedQuery;

    private IEnumerable<SelectOption<UploadState?>> UploadStateOptions =>
        new SelectOption<UploadState?>[] { new(null, L["AnyUploadState"]) }.Concat(
            Enum.GetValues<UploadState>()
                .Select(state => new SelectOption<UploadState?>(state, L.Localize(state)))
        );

    private IEnumerable<SelectOption<OnlineState?>> OnlineStateOptions =>
        new SelectOption<OnlineState?>[] { new(null, L["AnyOnlineState"]) }.Concat(
            Enum.GetValues<OnlineState>()
                .Select(state => new SelectOption<OnlineState?>(state, L.Localize(state)))
        );

    private IEnumerable<SelectOption<int?>> HosterRegistrationOptions =>
        new[] { new SelectOption<int?>(null, L["AnyHosterConfig"]) }.Concat(
            HosterRegistrations.Select(h => new SelectOption<int?>(
                h.Id,
                $"{h.Name} ({h.HosterName})"
            ))
        );

    private IEnumerable<SelectOption<int>> ReleaseGroupOptions =>
        new[] { new SelectOption<int>(0, L["AnyReleaseGroup"]) }.Concat(
            ReleaseGroups.Select(group => new SelectOption<int>(group.ReleaseGroupId, group.Name))
        );

    protected override void OnParametersSet()
    {
        if (Query == syncedQuery)
        {
            return;
        }

        syncedQuery = Query;
        model = UploadSearchFormModel.FromQuery(Query);
    }

    private async Task ApplyFiltersAsync()
    {
        await OnSearch.InvokeAsync(model.ToQuery());
    }

    private async Task ResetFiltersAsync()
    {
        if (!model.HasActiveFilters)
        {
            return;
        }

        model = new UploadSearchFormModel();

        await OnSearch.InvokeAsync(model.ToQuery());
    }
}
