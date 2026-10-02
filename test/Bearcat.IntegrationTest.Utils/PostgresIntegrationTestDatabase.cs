using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.IntegrationTest.Utils;

public sealed class PostgresIntegrationTestDatabase(
    PostgresIntegrationTestDatabaseServer server,
    string databaseName,
    string connectionString
) : BearcatIntegrationTestDatabase
{
    public override BearcatDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<BearcatDbContext>();
        optionsBuilder.UseBearcatPostgres(connectionString);

        return new BearcatDbContext(optionsBuilder.Options);
    }

    public override ValueTask DisposeAsync()
    {
        server.ReturnDatabaseToPool(databaseName);

        return ValueTask.CompletedTask;
    }
}
