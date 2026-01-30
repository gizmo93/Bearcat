using Bearcat.Domain.UseCases.ManageLinkCrypterContainers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class LinkCrypterContainerBackgroundTask(
    ILogger<LinkCrypterContainerBackgroundTask> logger,
    IServiceScopeFactory serviceScopeFactory)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Link Crypter Container Creation Background Task");

        while (true)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var archiveCreationService = scope.ServiceProvider.GetRequiredService<LinkCrypterContainerService>();
            await archiveCreationService.CreateMissingLinkCrypterContainersAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }
}
