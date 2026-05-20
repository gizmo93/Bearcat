using Bearcat.Domain.UseCases.ManageLinkCrypterContainers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class LinkCrypterContainerBackgroundTask(
    ILogger<LinkCrypterContainerBackgroundTask> logger,
    IServiceScopeFactory serviceScopeFactory
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Link Crypter Container Creation Background Task");
        await Task.Yield();

        while (true)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var archiveCreationService =
                scope.ServiceProvider.GetRequiredService<LinkCrypterContainerService>();

            try
            {
                await archiveCreationService.CreateMissingLinkCrypterContainersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(
                    e,
                    "An error occurred while creating missing link crypter containers for uploads"
                );
            }

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }
}
