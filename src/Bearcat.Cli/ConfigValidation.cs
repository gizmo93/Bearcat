using Npgsql;

namespace Bearcat.Cli;

public static class ConfigValidation
{
    public static bool ExecutableIsValid(string path)
    {
        return string.IsNullOrWhiteSpace(path) || File.Exists(path);
    }

    public static async Task<(bool Success, string? Error)> TestDatabaseConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception.Message);
        }
    }
}
