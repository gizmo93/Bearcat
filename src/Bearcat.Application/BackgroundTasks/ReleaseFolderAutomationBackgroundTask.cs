using Bearcat.Domain.UseCases.ManageReleases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ReleaseFolderAutomationBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ReleaseFolderAutomationBackgroundTask> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Release Folder Automation Background Task");
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var service =
                scope.ServiceProvider.GetRequiredService<AutomaticallyCreateReleasesService>();

            try
            {
                await service.ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Release folder automation scan failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }
}
