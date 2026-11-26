using Bearcat.Domain.UseCases.ManageUploads.EmptyFolderCleanup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.BackgroundTasks;

public class EmptyHosterFolderCleanupBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<EmptyHosterFolderCleanupBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Empty hoster folder cleanup";

    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(24);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var cleanupService = serviceProvider.GetRequiredService<EmptyHosterFolderCleanupService>();
        await cleanupService.DeleteEmptyFoldersAsync(stoppingToken);
    }
}
