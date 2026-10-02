using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.IntegrationTest.Utils;

public sealed class SqliteIntegrationTestDatabaseTemplate
{
    private const string TemplateFileName = "template.db";

    private static readonly Lazy<Task<SqliteIntegrationTestDatabaseTemplate>> CreatedTemplate = new(
        CreateTemplateAsync
    );

    private readonly string directoryPath;
    private readonly string templateFilePath;

    private SqliteIntegrationTestDatabaseTemplate(string directoryPath)
    {
        this.directoryPath = directoryPath;
        templateFilePath = Path.Combine(directoryPath, TemplateFileName);
    }

    public static async Task<SqliteIntegrationTestDatabase> CreateTestDatabaseAsync()
    {
        var template = await CreatedTemplate.Value;

        return template.CopyTemplateToNewDatabase();
    }

    public static async Task DeleteTemplateDirectoryAsync()
    {
        if (!CreatedTemplate.IsValueCreated)
        {
            return;
        }

        var template = await CreatedTemplate.Value;
        Directory.Delete(template.directoryPath, recursive: true);
    }

    private static async Task<SqliteIntegrationTestDatabaseTemplate> CreateTemplateAsync()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"bearcat-sqlite-tests-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(directoryPath);

        var template = new SqliteIntegrationTestDatabaseTemplate(directoryPath);
        await template.MigrateTemplateAsync();

        return template;
    }

    private async Task MigrateTemplateAsync()
    {
        var connectionString = SqliteIntegrationTestDatabase.CreateConnectionString(
            templateFilePath
        );

        await using (
            var dbContext = SqliteIntegrationTestDatabase.CreateDbContext(connectionString)
        )
        {
            await dbContext.Database.MigrateAsync();
        }

        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            await DeleteAllRowsAsync(connection);
            await ExecuteNonQueryAsync(connection, "PRAGMA wal_checkpoint(TRUNCATE);");
        }

        SqliteIntegrationTestDatabase.ClearConnectionPool(connectionString);
    }

    private static async Task DeleteAllRowsAsync(SqliteConnection connection)
    {
        var tableNames = await GetTableNamesAsync(connection);

        await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = OFF;");
        foreach (var tableName in tableNames)
        {
            await ExecuteNonQueryAsync(connection, $"DELETE FROM \"{tableName}\";");
        }
    }

    private static async Task<List<string>> GetTableNamesAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
              AND name <> '__EFMigrationsHistory';
            """;

        var tableNames = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames;
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private SqliteIntegrationTestDatabase CopyTemplateToNewDatabase()
    {
        var databaseFilePath = Path.Combine(directoryPath, $"{Guid.NewGuid():N}.db");
        File.Copy(templateFilePath, databaseFilePath);

        return new SqliteIntegrationTestDatabase(databaseFilePath);
    }
}
