using System.Collections.Concurrent;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace Bearcat.IntegrationTest.Utils;

public sealed class PostgresIntegrationTestDatabaseServer : IAsyncDisposable
{
    private const string MaintenanceDatabaseName = "bearcat";
    private const string TemplateDatabaseName = "bearcat_template";
    private const string TestDatabaseNamePrefix = "bearcat_test_";
    private const string Username = "bearcat";
    private const string Password = "bearcat123";
    private const string PostgreSqlImage = "postgres:18-alpine";
    private const int PostgreSqlContainerPort = 5432;
    private const int MaximumConnections = 300;

    private static readonly Lazy<Task<PostgresIntegrationTestDatabaseServer>> StartedServer = new(
        StartAsync
    );

    private readonly PostgreSqlContainer postgreSqlContainer;
    private readonly Respawner respawner;
    private readonly ConcurrentBag<string> unusedDatabaseNames = [];
    private int createdDatabaseCount;

    private PostgresIntegrationTestDatabaseServer(
        PostgreSqlContainer postgreSqlContainer,
        Respawner respawner
    )
    {
        this.postgreSqlContainer = postgreSqlContainer;
        this.respawner = respawner;
    }

    public static async Task<PostgresIntegrationTestDatabase> CreateTestDatabaseAsync()
    {
        var server = await StartedServer.Value;

        return await server.GetUnusedDatabaseAsync();
    }

    public static async Task DisposeStartedServerAsync()
    {
        if (!StartedServer.IsValueCreated)
        {
            return;
        }

        var server = await StartedServer.Value;
        await server.DisposeAsync();
    }

    public void ReturnDatabaseToPool(string databaseName)
    {
        unusedDatabaseNames.Add(databaseName);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await postgreSqlContainer.DisposeAsync();
    }

    private static async Task<PostgresIntegrationTestDatabaseServer> StartAsync()
    {
        var postgreSqlContainer = new PostgreSqlBuilder(PostgreSqlImage)
            .WithName($"bearcat-test-postgres-{Guid.NewGuid():N}")
            .WithDatabase(MaintenanceDatabaseName)
            .WithUsername(Username)
            .WithPassword(Password)
            .WithPortBinding(PostgreSqlContainerPort, true)
            .WithCommand("-c", $"max_connections={MaximumConnections}")
            .Build();
        await postgreSqlContainer.StartAsync();

        var templateConnectionString = CreateConnectionString(
            postgreSqlContainer,
            TemplateDatabaseName
        );
        await MigrateAsync(templateConnectionString);
        var respawner = await CreateRespawnerAsync(templateConnectionString);
        await DeleteAllRowsAsync(respawner, templateConnectionString);
        NpgsqlConnection.ClearAllPools();

        return new PostgresIntegrationTestDatabaseServer(postgreSqlContainer, respawner);
    }

    private static async Task MigrateAsync(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BearcatDbContext>();
        optionsBuilder.UseBearcatPostgres(connectionString);

        await using var dbContext = new BearcatDbContext(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync();
    }

    private static async Task<Respawner> CreateRespawnerAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        return await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = [new Table("public", "__EFMigrationsHistory")],
                FormatDeleteStatement = table =>
                    $"DELETE FROM \"{table.Schema}\".\"{table.Name}\";",
            }
        );
    }

    private static async Task ExecuteNonQueryAsync(string connectionString, string commandText)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string CreateConnectionString(
        PostgreSqlContainer postgreSqlContainer,
        string databaseName
    )
    {
        return new NpgsqlConnectionStringBuilder(postgreSqlContainer.GetConnectionString())
        {
            Database = databaseName,
        }.ConnectionString;
    }

    private async Task<PostgresIntegrationTestDatabase> GetUnusedDatabaseAsync()
    {
        if (unusedDatabaseNames.TryTake(out var unusedDatabaseName))
        {
            var unusedConnectionString = CreateConnectionString(
                postgreSqlContainer,
                unusedDatabaseName
            );
            await DeleteAllRowsAsync(respawner, unusedConnectionString);

            return new PostgresIntegrationTestDatabase(
                this,
                unusedDatabaseName,
                unusedConnectionString
            );
        }

        var databaseName = await CreateDatabaseFromTemplateAsync();

        return new PostgresIntegrationTestDatabase(
            this,
            databaseName,
            CreateConnectionString(postgreSqlContainer, databaseName)
        );
    }

    private static async Task DeleteAllRowsAsync(Respawner respawner, string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await respawner.ResetAsync(connection);
    }

    private async Task<string> CreateDatabaseFromTemplateAsync()
    {
        var databaseName =
            $"{TestDatabaseNamePrefix}{Interlocked.Increment(ref createdDatabaseCount)}";

        await ExecuteNonQueryAsync(
            postgreSqlContainer.GetConnectionString(),
            $"CREATE DATABASE {databaseName} TEMPLATE {TemplateDatabaseName}"
        );

        return databaseName;
    }
}
