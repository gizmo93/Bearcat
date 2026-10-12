using Bearcat.Domain.UseCases.UpdateQueryPlannerStatistics.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class SqliteQueryPlannerStatisticsRepository(BearcatDbContext dbContext)
    : IQueryPlannerStatisticsRepository
{
    private const string UpdateStatisticsOfAllTablesSql =
        "PRAGMA analysis_limit=1000; PRAGMA optimize=0x10002;";

    public async Task UpdateQueryPlannerStatisticsAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            UpdateStatisticsOfAllTablesSql,
            cancellationToken
        );
    }
}
