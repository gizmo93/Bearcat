using Bearcat.Domain.UseCases.ManageReleases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ReleaseClassificationBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ReleaseClassificationBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Release classification";

    protected override TimeSpan DefaultInterval => TimeSpan.FromMinutes(30);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var service = serviceProvider.GetRequiredService<ReleaseClassificationService>();
        await service.ProcessPendingClassificationsAsync(stoppingToken);
    }
}
