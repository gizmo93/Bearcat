using Bearcat.Infrastructure.Database;

namespace Bearcat.IntegrationTest.Utils;

public abstract class BearcatIntegrationTestDatabase : IAsyncDisposable
{
    public static async Task<BearcatIntegrationTestDatabase> CreateAsync(
        DatabaseProvider databaseProvider
    )
    {
        return databaseProvider switch
        {
            DatabaseProvider.Postgres =>
                await PostgresIntegrationTestDatabaseServer.CreateTestDatabaseAsync(),
            DatabaseProvider.Sqlite =>
                await SqliteIntegrationTestDatabaseTemplate.CreateTestDatabaseAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(databaseProvider)),
        };
    }

    public static async Task DisposeSharedResourcesAsync()
    {
        await PostgresIntegrationTestDatabaseServer.DisposeStartedServerAsync();
        await SqliteIntegrationTestDatabaseTemplate.DeleteTemplateDirectoryAsync();
    }

    public abstract BearcatDbContext CreateDbContext();

    public abstract ValueTask DisposeAsync();
}
