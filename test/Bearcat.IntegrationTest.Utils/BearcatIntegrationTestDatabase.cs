using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace Bearcat.IntegrationTest.Utils;

public sealed class BearcatIntegrationTestDatabase : IAsyncDisposable
{
    private const string DatabaseName = "bearcat";
    private const string Username = "bearcat";
    private const string Password = "bearcat123";
    private const string PostgreSqlImage = "postgres:18-alpine";
    private const int PostgreSqlContainerPort = 5432;

    private static readonly SemaphoreSlim StartLock = new(1, 1);
    private static readonly SemaphoreSlim ResetLock = new(1, 1);
    private static BearcatIntegrationTestDatabase? sharedDatabase;

    private readonly PostgreSqlContainer postgreSqlContainer;
    private Respawner? respawner;
    private bool disposed;

    private BearcatIntegrationTestDatabase(PostgreSqlContainer postgreSqlContainer)
    {
        this.postgreSqlContainer = postgreSqlContainer;
    }

    public string ConnectionString => postgreSqlContainer.GetConnectionString();

    public static async Task<BearcatDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default
    )
    {
        var database = await GetOrStartAsync(cancellationToken);
        return database.CreateDbContext();
    }

    public static async Task<BearcatIntegrationTestDatabase> GetOrStartAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (sharedDatabase is not null)
        {
            return sharedDatabase;
        }

        await StartLock.WaitAsync(cancellationToken);
        try
        {
            if (sharedDatabase is not null)
            {
                return sharedDatabase;
            }

            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            var database = new BearcatIntegrationTestDatabase(CreatePostgreSqlContainer());
            await database.StartAsync(cancellationToken);
            sharedDatabase = database;

            return database;
        }
        finally
        {
            StartLock.Release();
        }
    }

    public static async ValueTask DisposeSharedAsync()
    {
        if (sharedDatabase is null)
        {
            return;
        }

        await StartLock.WaitAsync();
        try
        {
            if (sharedDatabase is null)
            {
                return;
            }

            await sharedDatabase.DisposeAsync();
            sharedDatabase = null;
        }
        finally
        {
            StartLock.Release();
        }
    }

    public BearcatDbContext CreateDbContext()
    {
        ThrowIfDisposed();

        return CreateStartedDbContext();
    }

    private BearcatDbContext CreateStartedDbContext()
    {
        var options = new DbContextOptionsBuilder<BearcatDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new BearcatDbContext(options);
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (respawner is null)
        {
            throw new InvalidOperationException(
                "The test database must be started before it can be reset."
            );
        }

        await ResetLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await respawner.ResetAsync(connection);
        }
        finally
        {
            ResetLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await postgreSqlContainer.DisposeAsync();
    }

    private static PostgreSqlContainer CreatePostgreSqlContainer()
    {
        return new PostgreSqlBuilder(PostgreSqlImage)
            .WithName($"bearcat-test-postgres-{Guid.NewGuid():N}")
            .WithDatabase(DatabaseName)
            .WithUsername(Username)
            .WithPassword(Password)
            .WithPortBinding(PostgreSqlContainerPort, true)
            .Build();
    }

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        await postgreSqlContainer.StartAsync(cancellationToken);

        await using var dbContext = CreateStartedDbContext();
        await dbContext.Database.MigrateAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        respawner = await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = [new Table("public", "__EFMigrationsHistory")],
            }
        );
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
