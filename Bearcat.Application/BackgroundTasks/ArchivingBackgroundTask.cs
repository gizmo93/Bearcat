using Bearcat.Domain.UseCases.ManageArchives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ArchivingBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ArchivingBackgroundTask> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Distribution Packing Background Task");

        while (true)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var archiveCreationService = scope.ServiceProvider.GetRequiredService<ArchiveCreationService>();
            await archiveCreationService.ProcessAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }
}
