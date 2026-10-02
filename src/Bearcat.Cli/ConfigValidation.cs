using Npgsql;

namespace Bearcat.Cli;

public static class ConfigValidation
{
    public static bool ExecutableIsValid(string path)
    {
        return string.IsNullOrWhiteSpace(path) || File.Exists(path);
    }

    public static async Task<(bool Success, string? Error)> TestPostgresConnectionAsync(
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

    public static (bool Success, string? Error) TestSqliteFileLocation(string sqliteFilePath)
    {
        if (string.IsNullOrWhiteSpace(sqliteFilePath))
        {
            return (false, "The SQLite database file is required.");
        }

        if (!Path.IsPathFullyQualified(sqliteFilePath))
        {
            return (false, "The SQLite database file must be an absolute path.");
        }

        if (
            Directory.Exists(sqliteFilePath)
            || string.IsNullOrEmpty(Path.GetFileName(sqliteFilePath))
        )
        {
            return (false, "The SQLite database file must be a file, not a directory.");
        }

        var directory = Path.GetDirectoryName(sqliteFilePath)!;
        var probeFilePath = Path.Combine(directory, $".bearcat-write-test-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(probeFilePath, string.Empty);
            File.Delete(probeFilePath);
            return (true, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (false, $"The directory {directory} is not writable: {exception.Message}");
        }
    }
}
