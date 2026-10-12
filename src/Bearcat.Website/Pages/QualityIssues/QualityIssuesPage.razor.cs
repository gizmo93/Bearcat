using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Website.Formatting;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using Microsoft.AspNetCore.Components;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.QualityIssues;

public partial class QualityIssuesPage(
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager,
    TimeProvider timeProvider
) : ComponentBase
{
    private const string QualityIssuesPath = "/quality-issues";

    private IReadOnlyList<ReleaseQualityIssueQueueItemReadModel> items = [];
    private (int PageIndex, int PageSize)? loadedPage;
    private int totalCount;
    private int pageIndex;
    private int pageSize = SearchUrlParameters.DefaultPageSize;
    private bool isLoading = true;

    [SupplyParameterFromQuery(Name = "page")]
    public int? Page { get; set; }

    [SupplyParameterFromQuery(Name = "size")]
    public int? PageSize { get; set; }

    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));

    protected override async Task OnParametersSetAsync()
    {
        var requestedPage = (
            SearchUrlParameters.ParsePageIndex(Page),
            SearchUrlParameters.ParsePageSize(PageSize)
        );

        if (requestedPage == loadedPage)
        {
            return;
        }

        (pageIndex, pageSize) = requestedPage;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;

        try
        {
            var result = await operationRunner.RunAsync(
                (IReleaseReadRepository repository) =>
                    repository.GetQualityIssuesQueueAsync(
                        new ReleaseQualityIssueQueueSearchQuery(pageIndex, pageSize)
                    )
            );

            items = result.Items;
            totalCount = result.TotalCount;
            pageIndex = result.PageIndex;
            pageSize = result.PageSize;

            if (totalCount > 0 && pageIndex >= TotalPages)
            {
                pageIndex = TotalPages - 1;
                await LoadAsync();
                return;
            }

            loadedPage = (pageIndex, pageSize);
        }
        finally
        {
            isLoading = false;
        }
    }

    private string GetPageUrl(int page)
    {
        return SearchUrlParameters.Build(QualityIssuesPath, [], page, pageSize);
    }

    private void ChangePageSize(int selectedPageSize)
    {
        navigationManager.NavigateTo(
            SearchUrlParameters.Build(QualityIssuesPath, [], page: 1, selectedPageSize)
        );
    }

    private void OpenRelease(int releaseId)
    {
        navigationManager.NavigateTo($"/releases/{releaseId}");
    }

    private async Task RecheckAsync(int releaseId)
    {
        await operationRunner.RunAsync(
            (QualityGateService service) => service.RefreshAsync(releaseId)
        );

        await LoadAsync();
    }

    private async Task ApproveAsync(int releaseId)
    {
        await operationRunner.RunAsync(
            (QualityGateService service) => service.ApproveAsync(releaseId)
        );

        await LoadAsync();
    }

    private string HumanizeEvaluatedAt(DateTime evaluatedAt) => timeProvider.Humanize(evaluatedAt);
}
