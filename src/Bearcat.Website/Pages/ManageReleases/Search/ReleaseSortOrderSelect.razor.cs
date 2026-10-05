using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Website.Localization;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.Search;

public partial class ReleaseSortOrderSelect : ComponentBase
{
    private static readonly IReadOnlyList<ReleaseSearchSortOrder> SortOrders =
    [
        ReleaseSearchSortOrder.CreatedAtDescending,
        ReleaseSearchSortOrder.NameAscending,
        ReleaseSearchSortOrder.UploadsPostedAtDescending,
        ReleaseSearchSortOrder.OfflineUploadConfigCountDescending,
    ];

    [Parameter]
    public ReleaseSearchSortOrder Value { get; set; }

    [Parameter]
    public EventCallback<ReleaseSearchSortOrder> ValueChanged { get; set; }

    private IReadOnlyList<SelectOption<ReleaseSearchSortOrder>> SortOrderOptions =>
        SortOrders
            .Select(sortOrder => new SelectOption<ReleaseSearchSortOrder>(
                sortOrder,
                L.Localize(sortOrder)
            ))
            .ToList();

    private async Task SelectAsync(ReleaseSearchSortOrder sortOrder)
    {
        if (sortOrder == Value)
        {
            return;
        }

        await ValueChanged.InvokeAsync(sortOrder);
    }
}
