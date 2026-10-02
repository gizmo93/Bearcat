using EntityFramework.Exceptions.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database;

public static class SqliteDbContextOptionsBuilderExtensions
{
    private const string MigrationsAssemblyName = "Bearcat.Infrastructure.Migrations.Sqlite";

    private static readonly SqliteConnectionSetupInterceptor ConnectionSetupInterceptor = new();

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
                .UseExceptionProcessor()
                .AddInterceptors(ConnectionSetupInterceptor);
        }
    }
}
