using Bearcat.Domain.UseCases.ManageReleases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ReleaseInfoResolutionBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ReleaseInfoResolutionBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Release info resolution";

    protected override TimeSpan DefaultInterval => TimeSpan.FromMinutes(10);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var service = serviceProvider.GetRequiredService<ReleaseInfoResolutionService>();
        await service.ProcessMissingReleaseInfosAsync(stoppingToken);
    }
}
