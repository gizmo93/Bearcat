using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;

namespace Bearcat.Desktop;

public static class DesktopSettingsValidator
{
    public static async Task ValidateAsync(DesktopSettings settings)
    {
        RequireValue(settings.PostgresHost, "Postgres host is required.");
        RequireValue(settings.PostgresDatabase, "Database name is required.");
        RequireValue(settings.PostgresUsername, "Postgres username is required.");

        if (settings.WorkingDirectories.Count == 0)
        {
            throw new InvalidOperationException("At least one working directory is required.");
        }

        var missingDirectory = settings.WorkingDirectories.FirstOrDefault(directory =>
            !Directory.Exists(directory)
        );
        if (missingDirectory is not null)
        {
            throw new InvalidOperationException(
                $"Working directory does not exist: {missingDirectory}"
            );
        }

        if (!CommandExists(settings.RarPath))
        {
            throw new InvalidOperationException("RAR executable was not found.");
        }

        if (!CommandExists(settings.SevenZipPath))
        {
            throw new InvalidOperationException("7z executable was not found.");
        }

        await ValidatePostgresServerAsync(settings);
    }

    public static bool CommandExists(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        if (
            command.Contains(Path.DirectorySeparatorChar)
            || command.Contains(Path.AltDirectorySeparatorChar)
            || Path.IsPathRooted(command)
        )
        {
            return File.Exists(command);
        }

        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries
        );
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries
            )
            : [string.Empty];

        return paths.Any(path =>
            extensions.Any(extension => File.Exists(Path.Combine(path, command + extension)))
        );
    }

    private static void RequireValue(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static async Task ValidatePostgresServerAsync(DesktopSettings settings)
    {
        var builder = new NpgsqlConnectionStringBuilder(settings.CreateConnectionString())
        {
            Database = "postgres",
        };

        try
        {
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            // Only validate that PostgreSQL is reachable. Bearcat.Host creates the target DB.
        }
        catch (PostgresException exception) when (IsAuthenticationError(exception))
        {
            throw new InvalidOperationException(
                "Could not connect to PostgreSQL. Check username and password.",
                exception
            );
        }
        catch (PostgresException exception)
        {
            throw new InvalidOperationException(
                $"PostgreSQL rejected the connection with SQL state {exception.SqlState}.",
                exception
            );
        }
    }

    private static bool IsAuthenticationError(PostgresException exception)
    {
        return exception.SqlState is "28000" or "28P01";
    }
}
