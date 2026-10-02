using Bearcat.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.IntegrationTest.Utils;

public sealed class SqliteIntegrationTestDatabase(string databaseFilePath)
    : BearcatIntegrationTestDatabase
{
    private readonly string connectionString = CreateConnectionString(databaseFilePath);

    public override BearcatDbContext CreateDbContext()
    {
        return CreateDbContext(connectionString);
    }

    public override ValueTask DisposeAsync()
    {
        ClearConnectionPool(connectionString);
        File.Delete(databaseFilePath);
        File.Delete($"{databaseFilePath}-wal");
        File.Delete($"{databaseFilePath}-shm");

        return ValueTask.CompletedTask;
    }

    internal static string CreateConnectionString(string databaseFilePath)
    {
        return new SqliteConnectionStringBuilder { DataSource = databaseFilePath }.ToString();
    }

    internal static BearcatDbContext CreateDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BearcatDbContext>();
        optionsBuilder.UseBearcatSqlite(connectionString);

        return new BearcatDbContext(optionsBuilder.Options);
    }

    internal static void ClearConnectionPool(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        SqliteConnection.ClearPool(connection);
    }
}
