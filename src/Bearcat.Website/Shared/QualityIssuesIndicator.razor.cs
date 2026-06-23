using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Shared;

public partial class QualityIssuesIndicator(
    IServiceScopeFactory serviceScopeFactory,
    NavigationManager navigationManager
) : ComponentBase, IAsyncDisposable
{
    private readonly CancellationTokenSource pollingCancellation = new();
    private int openCount;
    private Task? pollingTask;

    private string BadgeText => openCount > 99 ? "99+" : openCount.ToString();

    protected override async Task OnInitializedAsync()
    {
        await RefreshCountAsync();
        pollingTask = PollCountAsync(pollingCancellation.Token);
    }

    private void OpenQualityIssues()
    {
        navigationManager.NavigateTo("/quality-issues");
    }

    private async Task RefreshCountAsync()
    {
        using var scope = serviceScopeFactory.CreateScope();
        var releaseReadRepository =
            scope.ServiceProvider.GetRequiredService<IReleaseReadRepository>();

        openCount = await releaseReadRepository.CountQualityIssuesQueueAsync();
    }

    private async Task PollCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(async () =>
                {
                    await RefreshCountAsync();
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        await pollingCancellation.CancelAsync();
        pollingCancellation.Dispose();

        if (pollingTask is not null)
        {
            try
            {
                await pollingTask;
            }
            catch (OperationCanceledException) { }
        }
    }
}
