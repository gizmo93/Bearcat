using Bearcat.Infrastructure.Database.MaterializedValues;
using EntityFramework.Exceptions.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database;

public static class SqliteDbContextOptionsBuilderExtensions
{
    private const string MigrationsAssemblyName = "Bearcat.Infrastructure.Migrations.Sqlite";

    private static readonly SqliteConnectionSetupInterceptor ConnectionSetupInterceptor = new();

    private static readonly MaterializedValuesSaveChangesInterceptor MaterializedValuesInterceptor =
        new();

    extension(DbContextOptionsBuilder builder)
    {
        public DbContextOptionsBuilder UseBearcatSqlite(string connectionString)
        {
            return builder
                .UseSqlite(
                    connectionString,
                    options =>
                        options
                            .MigrationsAssembly(MigrationsAssemblyName)
                            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                )
                .AddInterceptors(ConnectionSetupInterceptor, MaterializedValuesInterceptor)
                .UseExceptionProcessor();
        }
    }
}
