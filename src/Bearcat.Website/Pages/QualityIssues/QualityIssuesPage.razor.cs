using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Website.Formatting;
using Bearcat.Website.ScopedOperations;
using Microsoft.AspNetCore.Components;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.QualityIssues;

public partial class QualityIssuesPage(
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager,
    TimeProvider timeProvider
) : ComponentBase
{
    private IReadOnlyList<ReleaseQualityIssueQueueItemReadModel> items = [];
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;

        try
        {
            items = await operationRunner.RunAsync(
                (IReleaseReadRepository repository) => repository.GetQualityIssuesQueueAsync()
            );
        }
        finally
        {
            isLoading = false;
        }
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
