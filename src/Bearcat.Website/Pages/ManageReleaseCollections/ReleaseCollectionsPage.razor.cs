using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.Pages.ManageReleaseCollections.Search;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class ReleaseCollectionsPage(
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager
) : IReleaseCollectionSearchUrlValues
{
    private static readonly IReadOnlyList<ReleaseCollectionSearchSortOrder> SortOrders =
    [
        ReleaseCollectionSearchSortOrder.NameAscending,
        ReleaseCollectionSearchSortOrder.NameDescending,
        ReleaseCollectionSearchSortOrder.CreatedAtDescending,
        ReleaseCollectionSearchSortOrder.CreatedAtAscending,
    ];

    private readonly SearchResultSelection selection = new();
    private IReadOnlyList<ReleaseCollectionReadModel>? releaseCollections;
    private IReadOnlyList<ReleaseGroupReadModel> releaseGroups = [];
    private ReleaseCollectionSearchQuery searchQuery = new();
    private ReleaseOnlineStateCounts? onlineStateCounts;
    private SearchUrlState<ReleaseCollectionSearchQuery>? loadedState;
    private int totalCount;
    private int pageIndex;
    private int pageSize = SearchUrlParameters.DefaultPageSize;
    private bool isLoading;

    [SupplyParameterFromQuery(Name = "q")]
    public string? SearchTerm { get; set; }

    [SupplyParameterFromQuery(Name = "content")]
    public string? ReleaseContentType { get; set; }

    [SupplyParameterFromQuery(Name = "lang")]
    public string? Language { get; set; }

    [SupplyParameterFromQuery(Name = "state")]
    public string? OnlineState { get; set; }

    [SupplyParameterFromQuery(Name = "group")]
    public int? ReleaseGroupId { get; set; }

    [SupplyParameterFromQuery(Name = "sort")]
    public string? SortOrder { get; set; }

    [SupplyParameterFromQuery(Name = "page")]
    public int? Page { get; set; }

    [SupplyParameterFromQuery(Name = "size")]
    public int? PageSize { get; set; }

    private string ReleaseCollectionTotalCountText =>
        L[
            totalCount == 1 ? "ReleaseCollectionTotalCountOne" : "ReleaseCollectionTotalCount",
            totalCount.ToString("N0", CultureInfo.CurrentCulture)
        ];

    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));

    private IReadOnlyList<ReleaseCollectionReadModel> VisibleReleaseCollections =>
        releaseCollections ?? [];

    private IReadOnlyList<int> VisibleReleaseCollectionIds =>
        VisibleReleaseCollections
            .Select(releaseCollection => releaseCollection.ReleaseCollectionId)
            .ToList();

    private bool HasActiveFilters => searchQuery != EmptySearchQuery;

    private ReleaseCollectionSearchQuery EmptySearchQuery => new(SortOrder: searchQuery.SortOrder);

    private IReadOnlyList<SelectOption<ReleaseCollectionSearchSortOrder>> SortOrderOptions =>
        SortOrders
            .Select(sortOrder => new SelectOption<ReleaseCollectionSearchSortOrder>(
                sortOrder,
                L.Localize(sortOrder)
            ))
            .ToList();

    private string SectionClass =>
        selection.Count > 0
            ? "bearcat-releases-page space-y-4 pb-20"
            : "bearcat-releases-page space-y-4";

    protected override async Task OnInitializedAsync()
    {
        releaseGroups = await operationRunner.RunAsync(
            (IReleaseGroupReadRepository repository) => repository.GetAllAsync()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        var state = ReleaseCollectionSearchUrl.Parse(this);

        if (state == loadedState)
        {
            return;
        }

        if (loadedState is not null && loadedState.Query != state.Query)
        {
            selection.Clear();
        }

        searchQuery = state.Query;
        pageIndex = state.PageIndex;
        pageSize = state.PageSize;
        await RefreshReleaseCollectionsAsync();
    }

    private string GetPageUri(int page)
    {
        return ReleaseCollectionSearchUrl.Build(searchQuery, page, pageSize);
    }

    private async Task RefreshReleaseCollectionsAsync()
    {
        isLoading = true;

        try
        {
            var result = await operationRunner.RunAsync(
                (IReleaseCollectionReadRepository repository) =>
                    repository.SearchAsync(
                        searchQuery with
                        {
                            PageIndex = pageIndex,
                            PageSize = pageSize,
                        }
                    )
            );

            releaseCollections = result.Items;
            totalCount = result.TotalCount;
            pageIndex = result.PageIndex;
            pageSize = result.PageSize;
            selection.KeepOnly(VisibleReleaseCollectionIds);

            if (totalCount > 0 && pageIndex >= TotalPages)
            {
                pageIndex = TotalPages - 1;
                await RefreshReleaseCollectionsAsync();
                return;
            }

            onlineStateCounts = await operationRunner.RunAsync(
                (IReleaseCollectionReadRepository repository) =>
                    repository.CountByOnlineStateAsync(searchQuery)
            );

            loadedState = new SearchUrlState<ReleaseCollectionSearchQuery>(
                Query: searchQuery,
                PageIndex: pageIndex,
                PageSize: pageSize
            );
        }
        finally
        {
            isLoading = false;
        }
    }

    private Task ApplySearchAsync(ReleaseCollectionSearchQuery query)
    {
        return NavigateToSearchAsync(query, replaceHistoryEntry: false);
    }

    private Task ApplySearchTermAsync(string? searchTerm)
    {
        return ApplySearchAsync(searchQuery with { SearchTerm = searchTerm });
    }

    private Task ApplyTypedSearchTermAsync(string? searchTerm)
    {
        return NavigateToSearchAsync(
            searchQuery with
            {
                SearchTerm = searchTerm,
            },
            replaceHistoryEntry: true
        );
    }

    private Task ApplyOnlineStateAsync(OnlineState? onlineState)
    {
        return ApplySearchAsync(searchQuery with { OnlineState = onlineState });
    }

    private Task ApplySortOrderAsync(ReleaseCollectionSearchSortOrder sortOrder)
    {
        return ApplySearchAsync(searchQuery with { SortOrder = sortOrder });
    }

    private Task ResetFiltersAsync()
    {
        return ApplySearchAsync(EmptySearchQuery);
    }

    private async Task NavigateToSearchAsync(
        ReleaseCollectionSearchQuery query,
        bool replaceHistoryEntry
    )
    {
        var targetUri = navigationManager
            .ToAbsoluteUri(ReleaseCollectionSearchUrl.Build(query, page: 1, pageSize))
            .ToString();

        if (targetUri == navigationManager.Uri)
        {
            await RefreshReleaseCollectionsAsync();
            return;
        }

        navigationManager.NavigateTo(targetUri, replace: replaceHistoryEntry);
    }

    private void ChangePageSize(int selectedPageSize)
    {
        navigationManager.NavigateTo(
            ReleaseCollectionSearchUrl.Build(searchQuery, page: 1, selectedPageSize)
        );
    }
}
