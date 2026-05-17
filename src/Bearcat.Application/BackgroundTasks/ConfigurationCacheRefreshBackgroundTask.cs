using Bearcat.Abstractions.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ConfigurationCacheRefreshBackgroundTask(
    IApplicationConfigurationOverrideCache overrideCache,
    ILogger<ConfigurationCacheRefreshBackgroundTask> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Configuration Cache Refresh Background Task");
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            await overrideCache.RefreshAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
