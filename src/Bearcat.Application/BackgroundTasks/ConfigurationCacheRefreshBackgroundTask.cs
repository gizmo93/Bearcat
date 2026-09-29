using Bearcat.Abstractions.Configurations;
using Bearcat.Abstractions.Proxies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ConfigurationCacheRefreshBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ConfigurationCacheRefreshBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Configuration cache refresh";

    protected override TimeSpan DefaultInterval => TimeSpan.FromMinutes(5);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var overrideCache =
            serviceProvider.GetRequiredService<IApplicationConfigurationOverrideCache>();
        await overrideCache.RefreshAsync(stoppingToken);

        var proxyRoutingCache = serviceProvider.GetRequiredService<IProxyRoutingCache>();
        await proxyRoutingCache.RefreshAsync(stoppingToken);
    }
}
