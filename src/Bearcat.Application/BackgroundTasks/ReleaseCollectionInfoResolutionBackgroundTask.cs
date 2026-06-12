using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ReleaseCollectionInfoResolutionBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ReleaseCollectionInfoResolutionBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Release collection metadata resolution";

    protected override TimeSpan Interval => TimeSpan.FromMinutes(30);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var service = serviceProvider.GetRequiredService<ReleaseCollectionInfoResolutionService>();
        await service.ProcessMissingCollectionMetadataAsync(stoppingToken);
    }
}
