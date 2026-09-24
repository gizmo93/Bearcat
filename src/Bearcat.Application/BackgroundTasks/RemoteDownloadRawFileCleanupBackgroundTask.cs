using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.RawFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class RemoteDownloadRawFileCleanupBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RemoteDownloadRawFileCleanupBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Remote download raw file cleanup";

    protected override TimeSpan DefaultInterval => TimeSpan.FromMinutes(2);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var cleanupService =
            serviceProvider.GetRequiredService<RemoteDownloadRawFileCleanupService>();
        await cleanupService.ProcessAsync(stoppingToken);
    }
}
