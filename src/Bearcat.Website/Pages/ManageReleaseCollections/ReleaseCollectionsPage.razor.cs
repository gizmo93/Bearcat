using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Dto;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Primitives;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class ReleaseCollectionsPage(IScopedOperationRunner operationRunner)
{
    private readonly int[] pageSizes = [10, 20, 50, 100];

    private IReadOnlyList<ReleaseCollectionReadModel> releaseCollections = [];
    private IReadOnlyList<ReleaseGroupReadModel> releaseGroups = [];
    private string? searchTerm;
    private ReleaseContentType? selectedReleaseContentType;
    private int? selectedReleaseGroupId;
    private int totalCount;
    private int pageIndex;
    private int pageSize = 10;
    private bool isLoading;

    private int CurrentPage => totalCount == 0 ? 1 : pageIndex + 1;
    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
    private int FirstResult => totalCount == 0 ? 0 : pageIndex * pageSize + 1;
    private int LastResult => Math.Min(totalCount, (pageIndex + 1) * pageSize);
    private string CollectionsTableKey =>
        $"{pageIndex}-{pageSize}-{searchTerm}-{selectedReleaseContentType}-{selectedReleaseGroupId}";

    private IReadOnlyList<SelectOption<int?>> ReleaseGroupOptions =>
        [
            new(null, L["AllReleaseGroups"]),
            .. releaseGroups.Select(group => new SelectOption<int?>(
                group.ReleaseGroupId,
                group.Name
            )),
        ];

    private IReadOnlyList<SelectOption<ReleaseContentType?>> ReleaseContentTypeOptions =>
        [
            new(null, L["AnyReleaseContentType"]),
            .. Enum.GetValues<ReleaseContentType>()
                .Select(type => new SelectOption<ReleaseContentType?>(type, L.Localize(type))),
        ];

    private IReadOnlyList<SelectOption<int>> PageSizeOptions =>
        pageSizes.Select(size => new SelectOption<int>(size, size.ToString())).ToList();

    private IReadOnlyList<int?> PaginationItems
    {
        get
        {
            if (TotalPages <= 7)
            {
                return Enumerable.Range(1, TotalPages).Select(page => (int?)page).ToList();
            }

            var pages = new List<int?> { 1 };
            var start = Math.Max(2, CurrentPage - 1);
            var end = Math.Min(TotalPages - 1, CurrentPage + 1);

            if (start > 2)
            {
                pages.Add(null);
            }

            pages.AddRange(Enumerable.Range(start, end - start + 1).Select(page => (int?)page));

            if (end < TotalPages - 1)
            {
                pages.Add(null);
            }

            pages.Add(TotalPages);
            return pages;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        releaseGroups = await operationRunner.RunAsync(
            (IReleaseGroupReadRepository repository) => repository.GetAllAsync()
        );
        await RefreshAsync();
    }

    private async Task ApplySearchAsync()
    {
        pageIndex = 0;
        await RefreshAsync();
    }

    private async Task ResetSearchAsync()
    {
        searchTerm = null;
        selectedReleaseContentType = null;
        selectedReleaseGroupId = null;
        pageIndex = 0;
        await RefreshAsync();
    }

    private async Task OnPageSizeChangedAsync()
    {
        pageIndex = 0;
        await RefreshAsync();
    }

    private async Task GoToPreviousPageAsync()
    {
        if (pageIndex == 0)
        {
            return;
        }

        pageIndex--;
        await RefreshAsync();
    }

    private async Task GoToNextPageAsync()
    {
        if (CurrentPage >= TotalPages)
        {
            return;
        }

        pageIndex++;
        await RefreshAsync();
    }

    private async Task GoToPageAsync(int page)
    {
        pageIndex = page - 1;
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        isLoading = true;

        try
        {
            PagedResult<ReleaseCollectionReadModel> result = await operationRunner.RunAsync(
                (IReleaseCollectionReadRepository repository) =>
                    repository.SearchAsync(
                        new ReleaseCollectionSearchQuery(
                            SearchTerm: searchTerm,
                            ReleaseContentType: selectedReleaseContentType,
                            ReleaseGroupId: selectedReleaseGroupId,
                            PageIndex: pageIndex,
                            PageSize: pageSize
                        )
                    )
            );

            releaseCollections = result.Items;
            totalCount = result.TotalCount;
            pageIndex = result.PageIndex;
            pageSize = result.PageSize;

            if (totalCount > 0 && pageIndex >= TotalPages)
            {
                pageIndex = TotalPages - 1;
                await RefreshAsync();
            }
        }
        finally
        {
            isLoading = false;
        }
    }
}
