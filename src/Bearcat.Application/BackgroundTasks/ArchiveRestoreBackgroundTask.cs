using Bearcat.Domain.UseCases.DownloadArchivesFromMirror;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ArchiveRestoreBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ArchiveRestoreBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Archive restore";

    protected override TimeSpan DefaultInterval => TimeSpan.FromSeconds(60);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var archiveRestoreService = serviceProvider.GetRequiredService<ArchiveRestoreService>();
        await archiveRestoreService.ProcessAsync(stoppingToken);
    }
}
