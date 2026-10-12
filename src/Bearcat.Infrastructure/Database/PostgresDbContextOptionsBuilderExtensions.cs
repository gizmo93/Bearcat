using Bearcat.Infrastructure.Database.MaterializedValues;
using EntityFramework.Exceptions.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database;

public static class PostgresDbContextOptionsBuilderExtensions
{
    private const string MigrationsAssemblyName = "Bearcat.Infrastructure.Migrations.Postgres";

    private static readonly MaterializedValuesSaveChangesInterceptor MaterializedValuesInterceptor =
        new();

    extension(DbContextOptionsBuilder builder)
    {
        public DbContextOptionsBuilder UseBearcatPostgres(string? connectionString)
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            return builder
                .UseNpgsql(
                    connectionString,
                    options =>
                        options
                            .MigrationsAssembly(MigrationsAssemblyName)
                            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                )
                .AddInterceptors(MaterializedValuesInterceptor)
                .UseExceptionProcessor();
        }
    }
}
