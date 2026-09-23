using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.ReleaseCreation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class RemoteDownloadReleaseCreationBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RemoteDownloadReleaseCreationBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Remote download release creation";

    protected override TimeSpan DefaultInterval => TimeSpan.FromSeconds(20);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var creator = serviceProvider.GetRequiredService<RemoteDownloadReleaseCreator>();
        await creator.ProcessAsync(stoppingToken);
    }
}
