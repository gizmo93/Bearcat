using Bearcat.Abstractions.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ConfigurationCacheRefreshBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ConfigurationCacheRefreshBackgroundTask> logger
) : BearcatBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Configuration cache refresh";

    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var overrideCache =
            serviceProvider.GetRequiredService<IApplicationConfigurationOverrideCache>();
        await overrideCache.RefreshAsync(stoppingToken);
    }
}
