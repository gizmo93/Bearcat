using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Website.Formatting;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.QualityIssues;

public partial class QualityIssuesPage(
    IServiceScopeFactory serviceScopeFactory,
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
            using var scope = serviceScopeFactory.CreateScope();
            items = await scope
                .ServiceProvider.GetRequiredService<IReleaseReadRepository>()
                .GetQualityIssuesQueueAsync();
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
        using (var scope = serviceScopeFactory.CreateScope())
        {
            await scope
                .ServiceProvider.GetRequiredService<QualityGateService>()
                .RefreshAsync(releaseId);
        }

        await LoadAsync();
    }

    private async Task ApproveAsync(int releaseId)
    {
        using (var scope = serviceScopeFactory.CreateScope())
        {
            await scope
                .ServiceProvider.GetRequiredService<QualityGateService>()
                .ApproveAsync(releaseId);
        }

        await LoadAsync();
    }

    private string HumanizeEvaluatedAt(DateTime evaluatedAt) => timeProvider.Humanize(evaluatedAt);
}
