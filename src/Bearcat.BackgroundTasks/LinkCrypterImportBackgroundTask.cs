using Bearcat.Domain.UseCases.ImportLinkCrypterContainers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.BackgroundTasks;

public class LinkCrypterImportBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<LinkCrypterImportBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Link crypter import";

    protected override TimeSpan DefaultInterval => TimeSpan.FromSeconds(20);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var service = serviceProvider.GetRequiredService<LinkCrypterImportProcessingService>();
        await service.ProcessAsync(stoppingToken);
    }
}
