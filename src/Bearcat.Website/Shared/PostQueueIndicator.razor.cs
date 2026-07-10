using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Website.ScopedOperations;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class PostQueueIndicator(
    IScopedOperationRunner operationRunner,
    NavigationManager navigationManager
) : ComponentBase, IAsyncDisposable
{
    private readonly CancellationTokenSource pollingCancellation = new();
    private int openCount;
    private bool enabled = true;
    private Task? pollingTask;

    private string BadgeText => openCount > 99 ? "99+" : openCount.ToString();

    protected override async Task OnInitializedAsync()
    {
        await RefreshCountAsync();
        pollingTask = PollCountAsync(pollingCancellation.Token);
    }

    private void OpenPostQueue()
    {
        navigationManager.NavigateTo("/post-queue");
    }

    private async Task RefreshCountAsync()
    {
        await operationRunner.RunAsync(
            async (
                IApplicationConfigurationProvider configuration,
                IReleaseReadRepository releaseRepository,
                IReleaseCollectionReadRepository collectionRepository
            ) =>
            {
                enabled = configuration.GetValue<PostQueueConfiguration>(c => c.Enabled);

                openCount = enabled
                    ? await releaseRepository.CountPostQueueAsync()
                        + await collectionRepository.CountPostQueueAsync()
                    : 0;
            }
        );
    }

    private async Task PollCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
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
