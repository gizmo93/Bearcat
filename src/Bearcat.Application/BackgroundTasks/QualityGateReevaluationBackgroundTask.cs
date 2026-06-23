using Bearcat.Domain.UseCases.ManageReleases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class QualityGateReevaluationBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<QualityGateReevaluationBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Quality gate re-evaluation";

    protected override TimeSpan DefaultInterval => TimeSpan.FromMinutes(30);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var service = serviceProvider.GetRequiredService<QualityGateService>();
        await service.ReevaluatePendingReleasesAsync(stoppingToken);
    }
}
