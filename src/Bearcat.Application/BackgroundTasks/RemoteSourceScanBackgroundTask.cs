using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class RemoteSourceScanBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RemoteSourceScanBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Remote source scan";

    protected override TimeSpan DefaultInterval => TimeSpan.FromMinutes(2);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var service = serviceProvider.GetRequiredService<RemoteSourceScanService>();
        await service.ProcessAsync(stoppingToken);
    }
}
