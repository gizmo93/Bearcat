using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Generation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.BackgroundTasks;

public class ReleaseScreenshotGenerationBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ReleaseScreenshotGenerationBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Screenshot creation";

    protected override TimeSpan DefaultInterval => TimeSpan.FromMinutes(1);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var service = serviceProvider.GetRequiredService<ReleaseScreenshotGenerationService>();
        await service.ProcessPendingAsync(stoppingToken);
    }
}
