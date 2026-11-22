using System.Diagnostics;
using Bearcat.Domain.UseCases.UpdateQueryPlannerStatistics.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bearcat.Infrastructure.Database.Repositories;

public class SqliteQueryPlannerStatisticsRepository(
    BearcatDbContext dbContext,
    ILogger<SqliteQueryPlannerStatisticsRepository> logger
) : IQueryPlannerStatisticsRepository
{
    private const string UpdateStatisticsOfAllTablesSql =
        "PRAGMA analysis_limit=1000; PRAGMA optimize=0x10002;";

    public async Task UpdateQueryPlannerStatisticsAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating query planner statistics");
        var startTimestamp = Stopwatch.GetTimestamp();

        await dbContext.Database.ExecuteSqlRawAsync(
            UpdateStatisticsOfAllTablesSql,
            cancellationToken
        );

        logger.LogInformation(
            "Updated query planner statistics in {ElapsedMilliseconds} ms",
            (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds
        );
    }
}
