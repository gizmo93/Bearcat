using Bearcat.Domain.UseCases.ManageArchives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ArchiveCleanupBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ArchiveCleanupBackgroundTask> logger
) : BearcatBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Archive cleanup";

    protected override TimeSpan Interval => TimeSpan.FromMinutes(30);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var archiveCleanupService = serviceProvider.GetRequiredService<ArchiveCleanupService>();
        await archiveCleanupService.ProcessAsync(stoppingToken);
    }
}
