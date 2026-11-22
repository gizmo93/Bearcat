using System.Diagnostics;
using Bearcat.Domain.UseCases.CompactDatabase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bearcat.Infrastructure.Database.Repositories;

public class SqliteDatabaseCompactionRepository(
    BearcatDbContext dbContext,
    ILogger<SqliteDatabaseCompactionRepository> logger
) : IDatabaseCompactionRepository
{
    private const double MinimumFreePageRatioForCompaction = 0.2;

    private const string FreePageRatioSql =
        "SELECT CAST(freelist_count AS REAL) / page_count AS \"Value\" "
        + "FROM pragma_freelist_count, pragma_page_count";

    private const string CompactDatabaseSql = "VACUUM; PRAGMA wal_checkpoint(TRUNCATE);";

    public async Task CompactDatabaseWithManyFreePagesAsync(CancellationToken cancellationToken)
    {
        var freePageRatio = await dbContext
            .Database.SqlQueryRaw<double>(FreePageRatioSql)
            .SingleAsync(cancellationToken);

        if (freePageRatio < MinimumFreePageRatioForCompaction)
        {
            return;
        }

        logger.LogInformation(
            "Compacting database because {FreePagePercentage} % of its pages are free",
            (int)(freePageRatio * 100)
        );

        var startTimestamp = Stopwatch.GetTimestamp();

        await dbContext.Database.ExecuteSqlRawAsync(CompactDatabaseSql, cancellationToken);

        logger.LogInformation(
            "Compacted database in {ElapsedMilliseconds} ms",
            (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds
        );
    }
}
