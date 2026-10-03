using Bearcat.Abstractions.Archiver;
using Bearcat.Domain.UseCases.ManageArchives.ReadModels;
using Bearcat.Domain.UseCases.ManageArchives.Repositories;
using Bearcat.Domain.UseCases.ManageArchives.Search;
using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageArchives;

public partial class AllArchivesPage(
    DialogService dialogService,
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager
) : IArchiveSearchUrlValues
{
    private const string ArchiveListGridClass =
        "lg:grid-cols-[minmax(0,1.8fr)_minmax(0,1fr)_minmax(0,0.8fr)_minmax(0,0.5fr)_minmax(0,0.8fr)_minmax(0,0.7fr)_56px]";

    private IReadOnlyList<ArchiveListItemReadModel> archives = [];
    private IReadOnlyList<ArchiverDto> archivers = [];
    private Dictionary<string, string> archiverDisplayNameByClassName = [];
    private IReadOnlyList<ReleaseGroupReadModel> releaseGroups = [];
    private ArchiveSearchQuery searchQuery = new();
    private SearchUrlState<ArchiveSearchQuery>? loadedState;
    private int totalCount;
    private int pageIndex;
    private int pageSize = SearchUrlParameters.DefaultPageSize;
    private bool isLoading;

    [SupplyParameterFromQuery(Name = "q")]
    public string? SearchTerm { get; set; }

    [SupplyParameterFromQuery(Name = "state")]
    public string? ArchiveState { get; set; }

    [SupplyParameterFromQuery(Name = "archiver")]
    public string? ArchiverName { get; set; }

    [SupplyParameterFromQuery(Name = "group")]
    public int? ReleaseGroupId { get; set; }

    [SupplyParameterFromQuery(Name = "disk")]
    public string? OnDiskFilter { get; set; }

    [SupplyParameterFromQuery(Name = "page")]
    public int? Page { get; set; }

    [SupplyParameterFromQuery(Name = "size")]
    public int? PageSize { get; set; }

    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));

    protected override async Task OnInitializedAsync()
    {
        archivers = operationRunner.Run(
            (IReleaseReadRepository repository) => repository.GetArchiverFilterOptions()
        );
        archiverDisplayNameByClassName = archivers.ToDictionary(
            archiver => archiver.ClassName,
            archiver => archiver.Name
        );
        releaseGroups = await operationRunner.RunAsync(
            (IReleaseGroupReadRepository repository) => repository.GetAllAsync()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        var state = ArchiveSearchUrl.Parse(this);

        if (state == loadedState)
        {
            return;
        }

        searchQuery = state.Query;
        pageIndex = state.PageIndex;
        pageSize = state.PageSize;
        await RefreshArchivesAsync();
    }

    private static string GetReleaseArchivesUrl(ArchiveListItemReadModel archive)
    {
        return $"/releases/{archive.ReleaseId}?tab=archives&archiveConfigId={archive.ArchiveConfigId}";
    }

    private string GetArchiverDisplayName(string archiverClassName)
    {
        return archiverDisplayNameByClassName.GetValueOrDefault(
            archiverClassName,
            archiverClassName
        );
    }

    private string GetPageUrl(int page)
    {
        return ArchiveSearchUrl.Build(searchQuery, page, pageSize);
    }

    private async Task RefreshArchivesAsync()
    {
        isLoading = true;

        try
        {
            var result = await operationRunner.RunAsync(
                (IArchiveReadRepository repository) =>
                    repository.SearchArchivesAsync(
                        searchQuery with
                        {
                            PageIndex = pageIndex,
                            PageSize = pageSize,
                        }
                    )
            );

            archives = result.Items;
            totalCount = result.TotalCount;
            pageIndex = result.PageIndex;
            pageSize = result.PageSize;

            if (totalCount > 0 && pageIndex >= TotalPages)
            {
                pageIndex = TotalPages - 1;
                await RefreshArchivesAsync();
                return;
            }

            loadedState = new SearchUrlState<ArchiveSearchQuery>(searchQuery, pageIndex, pageSize);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ApplySearchAsync(ArchiveSearchQuery query)
    {
        var targetUri = navigationManager
            .ToAbsoluteUri(ArchiveSearchUrl.Build(query, page: 1, pageSize))
            .ToString();

        if (targetUri == navigationManager.Uri)
        {
            await RefreshArchivesAsync();
            return;
        }

        navigationManager.NavigateTo(targetUri);
    }

    private void ChangePageSize(int selectedPageSize)
    {
        navigationManager.NavigateTo(
            ArchiveSearchUrl.Build(searchQuery, page: 1, selectedPageSize)
        );
    }

    private async Task ShowArchiveDialogAsync(int archiveId)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ArchiveDetailDialog.ArchiveId)] = archiveId,
        };

        await dialogService.OpenAsync<ArchiveDetailDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["ArchiveTitle", archiveId],
                Description = L["ArchiveDetailDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );
    }
}
