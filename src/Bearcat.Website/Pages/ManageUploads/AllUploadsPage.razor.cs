using Bearcat.Domain.UseCases.ManageArchives.Search;
using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Bearcat.Website.Pages.ManageArchives;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageUploads;

public partial class AllUploadsPage(
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager
) : IUploadSearchUrlValues
{
    private const string UploadListGridClass =
        "lg:grid-cols-[minmax(0,1.6fr)_minmax(0,1.1fr)_minmax(0,0.8fr)_minmax(0,0.8fr)_minmax(0,1fr)_minmax(0,0.5fr)]";

    private IReadOnlyList<UploadReadModel> uploads = [];
    private IReadOnlyList<HosterRegistrationReadModel> hosterRegistrations = [];
    private IReadOnlyList<ReleaseGroupReadModel> releaseGroups = [];
    private UploadSearchQuery searchQuery = new();
    private SearchUrlState<UploadSearchQuery>? loadedState;
    private int totalCount;
    private int pageIndex;
    private int pageSize = SearchUrlParameters.DefaultPageSize;
    private bool isLoading;

    [SupplyParameterFromQuery(Name = "q")]
    public string? SearchTerm { get; set; }

    [SupplyParameterFromQuery(Name = "state")]
    public string? UploadState { get; set; }

    [SupplyParameterFromQuery(Name = "online")]
    public string? OnlineState { get; set; }

    [SupplyParameterFromQuery(Name = "hoster")]
    public int? HosterRegistrationId { get; set; }

    [SupplyParameterFromQuery(Name = "group")]
    public int? ReleaseGroupId { get; set; }

    [SupplyParameterFromQuery(Name = "page")]
    public int? Page { get; set; }

    [SupplyParameterFromQuery(Name = "size")]
    public int? PageSize { get; set; }

    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));

    protected override async Task OnInitializedAsync()
    {
        hosterRegistrations = await operationRunner.RunAsync(
            (IHosterConfigurationReadRepository repository) => repository.GetAllRegistrationsAsync()
        );
        releaseGroups = await operationRunner.RunAsync(
            (IReleaseGroupReadRepository repository) => repository.GetAllAsync()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        var state = UploadSearchUrl.Parse(this);

        if (state == loadedState)
        {
            return;
        }

        searchQuery = state.Query;
        pageIndex = state.PageIndex;
        pageSize = state.PageSize;
        await RefreshUploadsAsync();
    }

    private static string GetReleaseUploadsUrl(UploadReadModel upload)
    {
        return $"/releases/{upload.ReleaseId}?tab=uploads&uploadConfigId={upload.UploadConfigId}";
    }

    private static string GetArchiveSearchUrl(int archiveId)
    {
        return ArchiveSearchUrl.Build(
            new ArchiveSearchQuery(SearchTerm: $"#{archiveId}"),
            page: 1,
            SearchUrlParameters.DefaultPageSize
        );
    }

    private string GetPageUrl(int page)
    {
        return UploadSearchUrl.Build(searchQuery, page, pageSize);
    }

    private async Task RefreshUploadsAsync()
    {
        isLoading = true;

        try
        {
            var result = await operationRunner.RunAsync(
                (IUploadReadRepository repository) =>
                    repository.SearchUploadsAsync(
                        searchQuery with
                        {
                            PageIndex = pageIndex,
                            PageSize = pageSize,
                        }
                    )
            );

            uploads = result.Items;
            totalCount = result.TotalCount;
            pageIndex = result.PageIndex;
            pageSize = result.PageSize;

            if (totalCount > 0 && pageIndex >= TotalPages)
            {
                pageIndex = TotalPages - 1;
                await RefreshUploadsAsync();
                return;
            }

            loadedState = new SearchUrlState<UploadSearchQuery>(searchQuery, pageIndex, pageSize);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ApplySearchAsync(UploadSearchQuery query)
    {
        var targetUri = navigationManager
            .ToAbsoluteUri(UploadSearchUrl.Build(query, page: 1, pageSize))
            .ToString();

        if (targetUri == navigationManager.Uri)
        {
            await RefreshUploadsAsync();
            return;
        }

        navigationManager.NavigateTo(targetUri);
    }

    private void ChangePageSize(int selectedPageSize)
    {
        navigationManager.NavigateTo(UploadSearchUrl.Build(searchQuery, page: 1, selectedPageSize));
    }
}
