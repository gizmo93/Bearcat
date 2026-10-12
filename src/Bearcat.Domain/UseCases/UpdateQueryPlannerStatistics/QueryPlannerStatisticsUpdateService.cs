using System.Diagnostics;
using Bearcat.Domain.UseCases.UpdateQueryPlannerStatistics.Repositories;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.UpdateQueryPlannerStatistics;

public class QueryPlannerStatisticsUpdateService(
    IQueryPlannerStatisticsRepository repository,
    ILogger<QueryPlannerStatisticsUpdateService> logger
)
{
    public async Task UpdateQueryPlannerStatisticsAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating query planner statistics");
        var startTimestamp = Stopwatch.GetTimestamp();

        await repository.UpdateQueryPlannerStatisticsAsync(cancellationToken);

        logger.LogInformation(
            "Updated query planner statistics in {ElapsedMilliseconds} ms",
            (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds
        );
    }
}
