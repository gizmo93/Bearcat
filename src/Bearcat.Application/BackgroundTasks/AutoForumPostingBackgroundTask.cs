using Bearcat.Domain.UseCases.PostToForums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class AutoForumPostingBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<AutoForumPostingBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Automatic forum posting";

    protected override TimeSpan DefaultInterval => TimeSpan.FromMinutes(15);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var runner = serviceProvider.GetRequiredService<AutoForumPostingRunner>();
        var result = await runner.RunAsync(stoppingToken);

        foreach (var failure in result.Failures)
        {
            logger.LogWarning(
                "Automatic forum posting of release {ReleaseName} to {DistributionSiteRegistrationName} failed: {Message}",
                failure.ReleaseName,
                failure.DistributionSiteRegistrationName,
                string.Join(" ", failure.Messages)
            );
        }

        if (result.PostedCount > 0 || result.FailedCount > 0)
        {
            logger.LogInformation(
                "Automatic forum posting created {PostedCount} posts and failed {FailedCount} times",
                result.PostedCount,
                result.FailedCount
            );
        }
    }
}
