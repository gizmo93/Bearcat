using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.UseCases.ManageArchives;
using Bearcat.Domain.UseCases.ManageReleases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ArchiveCleanupBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ArchiveCleanupBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Auto cleanup";

    protected override TimeSpan DefaultInterval => TimeSpan.FromMinutes(30);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var overrideCache =
            serviceProvider.GetRequiredService<IApplicationConfigurationOverrideCache>();

        if (!overrideCache.IsInitialized)
        {
            logger.LogDebug("Auto cleanup skipped until configuration cache is initialized");
            return;
        }

        var releaseFolderRetirementService =
            serviceProvider.GetRequiredService<ReleaseFolderRetirementService>();
        await releaseFolderRetirementService.ProcessAsync(stoppingToken);

        var archiveCleanupService = serviceProvider.GetRequiredService<ArchiveCleanupService>();
        await archiveCleanupService.ProcessAsync(stoppingToken);
    }
}
