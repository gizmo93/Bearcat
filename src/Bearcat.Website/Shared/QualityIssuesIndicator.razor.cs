using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Website.ScopedOperations;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class QualityIssuesIndicator(
    IScopedOperationRunner operationRunner,
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
        openCount = await operationRunner.RunAsync(
            (IReleaseReadRepository repository) => repository.CountQualityIssuesQueueAsync(pollingCancellation.Token)
        );
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
