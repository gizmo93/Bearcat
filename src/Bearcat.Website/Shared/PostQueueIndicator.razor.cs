using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Shared;

public partial class PostQueueIndicator(
    IServiceScopeFactory serviceScopeFactory,
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
        using var scope = serviceScopeFactory.CreateScope();
        var configuration =
            scope.ServiceProvider.GetRequiredService<IApplicationConfigurationProvider>();

        enabled = configuration.GetValue<PostQueueConfiguration>(c => c.Enabled);

        if (!enabled)
        {
            openCount = 0;
            return;
        }

        var releaseReadRepository =
            scope.ServiceProvider.GetRequiredService<IReleaseReadRepository>();
        var collectionReadRepository =
            scope.ServiceProvider.GetRequiredService<IReleaseCollectionReadRepository>();

        openCount =
            await releaseReadRepository.CountPostQueueAsync()
            + await collectionReadRepository.CountPostQueueAsync();
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
