using Bearcat.Domain.UseCases.ManageArchives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ArchiveCleanupBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ArchiveCleanupBackgroundTask> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Archive Cleanup Background Task");
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var archiveCleanupService =
                scope.ServiceProvider.GetRequiredService<ArchiveCleanupService>();
            await archiveCleanupService.ProcessAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }
}
