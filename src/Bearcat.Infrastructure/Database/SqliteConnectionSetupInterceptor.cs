using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bearcat.Infrastructure.Database;

public sealed class SqliteConnectionSetupInterceptor : DbConnectionInterceptor
{
    private const string ConnectionPragmas =
        "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=30000;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        var sqliteConnection = (SqliteConnection)connection;
        RegisterUnicodeLowerFunction(sqliteConnection);

        using var command = sqliteConnection.CreateCommand();
        command.CommandText = ConnectionPragmas;
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        var sqliteConnection = (SqliteConnection)connection;
        RegisterUnicodeLowerFunction(sqliteConnection);

        await using var command = sqliteConnection.CreateCommand();
        command.CommandText = ConnectionPragmas;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void RegisterUnicodeLowerFunction(SqliteConnection sqliteConnection)
    {
        sqliteConnection.CreateFunction(
            "lower",
            (string? value) => value?.ToLowerInvariant(),
            isDeterministic: true
        );
    }
}
