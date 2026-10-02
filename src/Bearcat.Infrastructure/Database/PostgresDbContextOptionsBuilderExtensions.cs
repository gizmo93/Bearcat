using EntityFramework.Exceptions.PostgreSQL;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database;

public static class PostgresDbContextOptionsBuilderExtensions
{
    private const string MigrationsAssemblyName = "Bearcat.Infrastructure.Migrations.Postgres";

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
                .UseExceptionProcessor();
        }
    }
}
