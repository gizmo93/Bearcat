using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.UseCases.ManageArchives.Search;
using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageArchives;

public partial class ArchiveSearchForm : ComponentBase
{
    [Parameter]
    public IReadOnlyList<ArchiverDto> Archivers { get; set; } = [];

    [Parameter]
    public IReadOnlyList<ReleaseGroupReadModel> ReleaseGroups { get; set; } = [];

    [Parameter]
    public ArchiveSearchQuery Query { get; set; } = new();

    [Parameter]
    public EventCallback<ArchiveSearchQuery> OnSearch { get; set; }

    private ArchiveSearchFormModel model = new();
    private ArchiveSearchQuery? syncedQuery;

    private IEnumerable<SelectOption<ArchiveState?>> ArchiveStateOptions =>
        new SelectOption<ArchiveState?>[] { new(null, L["AnyArchiveState"]) }.Concat(
            Enum.GetValues<ArchiveState>()
                .Select(state => new SelectOption<ArchiveState?>(state, L.Localize(state)))
        );

    private IEnumerable<SelectOption<string>> ArchiverOptions =>
        new[] { new SelectOption<string>(string.Empty, L["AnyArchiver"]) }.Concat(
            Archivers.Select(a => new SelectOption<string>(
                a.ClassName,
                $"{a.Name} ({a.FileExtension})"
            ))
        );

    private IEnumerable<SelectOption<int>> ReleaseGroupOptions =>
        new[] { new SelectOption<int>(0, L["AnyReleaseGroup"]) }.Concat(
            ReleaseGroups.Select(group => new SelectOption<int>(group.ReleaseGroupId, group.Name))
        );

    private IEnumerable<SelectOption<ArchiveOnDiskFilter?>> OnDiskFilterOptions =>
        [
            new(null, L["All"]),
            new(ArchiveOnDiskFilter.OnDisk, L["Yes"]),
            new(ArchiveOnDiskFilter.NotOnDisk, L["No"]),
        ];

    protected override void OnParametersSet()
    {
        if (Query == syncedQuery)
        {
            return;
        }

        syncedQuery = Query;
        model = ArchiveSearchFormModel.FromQuery(Query);
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

        model = new ArchiveSearchFormModel();

        await OnSearch.InvokeAsync(model.ToQuery());
    }
}
