using Bearcat.Domain.UseCases.ManageLinkCrypterContainers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class LinkCrypterContainerBackgroundTask(
    ILogger<LinkCrypterContainerBackgroundTask> logger,
    IServiceScopeFactory serviceScopeFactory
) : BearcatBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Link crypter container creation";

    protected override TimeSpan Interval => TimeSpan.FromSeconds(20);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var linkCrypterContainerService =
            serviceProvider.GetRequiredService<LinkCrypterContainerService>();
        await linkCrypterContainerService.CreateMissingLinkCrypterContainersAsync(stoppingToken);
    }
}
