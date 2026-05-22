using Bearcat.Domain.UseCases.ManageReleases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ReleaseFolderAutomationBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ReleaseFolderAutomationBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Release folder automation";

    protected override TimeSpan Interval => TimeSpan.FromMinutes(2);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var service = serviceProvider.GetRequiredService<AutomaticallyCreateReleasesService>();
        await service.ProcessAsync(stoppingToken);
    }
}
