using Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.VerificationAndExtraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class RemoteDownloadVerificationAndExtractionBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RemoteDownloadVerificationAndExtractionBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Remote download verification and extraction";

    protected override TimeSpan DefaultInterval => TimeSpan.FromSeconds(20);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var verificationAndExtractionService =
            serviceProvider.GetRequiredService<RemoteDownloadVerificationAndExtractionService>();
        await verificationAndExtractionService.ProcessAsync(stoppingToken);
    }
}
