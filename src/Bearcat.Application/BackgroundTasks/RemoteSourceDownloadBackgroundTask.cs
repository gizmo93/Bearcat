using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Downloading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class RemoteSourceDownloadBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RemoteSourceDownloadBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Remote source download";

    protected override TimeSpan DefaultInterval => TimeSpan.FromSeconds(20);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var service = serviceProvider.GetRequiredService<RemoteSourceDownloadService>();
        await service.ProcessAsync(stoppingToken);
    }
}
