using Bearcat.Domain.UseCases.ManageArchives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ArchivingBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ArchivingBackgroundTask> logger
) : BearcatBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Archive creation";

    protected override TimeSpan Interval => TimeSpan.FromSeconds(20);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var archiveCreationService = serviceProvider.GetRequiredService<ArchiveCreationService>();
        await archiveCreationService.ProcessAsync(stoppingToken);
    }
}
