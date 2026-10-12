using Bearcat.Domain.UseCases.UpdateQueryPlannerStatistics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.BackgroundTasks;

public class SqliteQueryPlannerStatisticsBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<SqliteQueryPlannerStatisticsBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "SQLite query planner statistics";

    protected override TimeSpan DefaultInterval => TimeSpan.FromDays(1);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var service = serviceProvider.GetRequiredService<QueryPlannerStatisticsUpdateService>();
        await service.UpdateQueryPlannerStatisticsAsync(stoppingToken);
    }
}
