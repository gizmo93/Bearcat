using System.Globalization;
using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Shared.PostQueue;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Website.ScopedOperations;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class PostQueueIndicator(
    IScopedOperationRunner operationRunner,
    PostQueueCountCache postQueueCountCache,
    NavigationManager navigationManager
) : ComponentBase, IAsyncDisposable
{
    private readonly CancellationTokenSource pollingCancellation = new();
    private int openCount;
    private bool enabled = true;
    private Task? pollingTask;

    private string BadgeText =>
        openCount > 99 ? "99+" : openCount.ToString(CultureInfo.CurrentCulture);

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
        enabled = operationRunner.Run(
            (IApplicationConfigurationProvider configuration) =>
                configuration.GetValue<PostQueueConfiguration>(c => c.Enabled)
        );

        openCount = enabled
            ? await postQueueCountCache.GetOrCountOpenItemsAsync(
                CountOpenItemsAsync,
                pollingCancellation.Token
            )
            : 0;
    }

    private async Task<int> CountOpenItemsAsync(CancellationToken cancellationToken)
    {
        return await operationRunner.RunAsync(
            async (
                IReleaseReadRepository releaseRepository,
                IReleaseCollectionReadRepository collectionRepository
            ) =>
                await releaseRepository.CountPostQueueAsync(cancellationToken)
                + await collectionRepository.CountPostQueueAsync(cancellationToken)
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
        GC.SuppressFinalize(this);

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
