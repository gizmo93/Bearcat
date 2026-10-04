using System.Globalization;
using Npgsql;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Bearcat.Cli.Commands;

public sealed class SetupCommand : AsyncCommand
{
    private const string HostExecutableName = "Bearcat.Host.exe";

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken
    )
    {
        if (!ElevationGuard.EnsureElevated())
        {
            return 1;
        }

        AnsiConsole.MarkupLine("[bold]Bearcat Windows Service setup[/]");
        AnsiConsole.WriteLine();

        if (
            File.Exists(BearcatPaths.WindowsServiceConfigPath)
            && !await AnsiConsole.ConfirmAsync(
                $"{BearcatPaths.WindowsServiceConfigPath} already exists. Overwrite?",
                defaultValue: false,
                cancellationToken: cancellationToken
            )
        )
        {
            AnsiConsole.MarkupLine("[yellow]Aborted.[/]");
            return 1;
        }

        var databaseProvider = await AnsiConsole.PromptAsync(
            new SelectionPrompt<DatabaseProvider>()
                .Title("Database:")
                .AddChoices(GetDatabaseProviderChoicesWithPreselectedFirst())
                .UseConverter(provider =>
                    provider == DatabaseProvider.Sqlite
                        ? $"{GetDisplayName(provider)} (recommended)"
                        : GetDisplayName(provider)
                ),
            cancellationToken
        );
        AnsiConsole.MarkupLineInterpolated(
            CultureInfo.InvariantCulture,
            $"Database: {GetDisplayName(databaseProvider)}"
        );

        var database =
            databaseProvider == DatabaseProvider.Sqlite
                ? await PromptSqliteDatabaseAsync(cancellationToken)
                : await PromptPostgresDatabaseAsync(cancellationToken);
        if (database is null)
        {
            return 1;
        }

        var sevenZipPath = await AnsiConsole.PromptAsync(
            new TextPrompt<string>("Path to 7z executable (empty = use PATH):")
                .AllowEmpty()
                .Validate(ValidateExecutable),
            cancellationToken
        );

        var rarPath = await AnsiConsole.PromptAsync(
            new TextPrompt<string>("Path to rar executable (empty = use PATH):")
                .AllowEmpty()
                .Validate(ValidateExecutable),
            cancellationToken
        );

        var workingDirectories = new List<string>();
        while (true)
        {
            var prompt = new TextPrompt<string>(
                workingDirectories.Count == 0
                    ? "Working directory:"
                    : "Additional working directory (empty = done):"
            ).Validate(ValidateWorkingDirectory);

            if (workingDirectories.Count > 0)
            {
                prompt.AllowEmpty();
            }

            var workingDirectory = (
                await AnsiConsole.PromptAsync(prompt, cancellationToken)
            ).Trim();
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                break;
            }

            if (workingDirectories.Contains(workingDirectory))
            {
                AnsiConsole.MarkupLine("[yellow]Already added.[/]");
                continue;
            }

            if (!Directory.Exists(workingDirectory))
            {
                if (
                    !await AnsiConsole.ConfirmAsync(
                        $"{workingDirectory} does not exist. Create it?",
                        cancellationToken: cancellationToken
                    )
                )
                {
                    continue;
                }

                Directory.CreateDirectory(workingDirectory);
            }

            workingDirectories.Add(workingDirectory);
        }

        var webPort = await AnsiConsole.PromptAsync(
            new TextPrompt<int>("Web port:").DefaultValue(17208),
            cancellationToken
        );
        var urls = $"http://127.0.0.1:{webPort}";

        new ServiceConfigFile
        {
            Database = database,
            Archivers = { RarPath = rarPath, SevenZipPath = sevenZipPath },
            WorkingDirectories = workingDirectories,
            Urls = urls,
        }.Save(BearcatPaths.WindowsServiceConfigPath);
        AnsiConsole.MarkupLineInterpolated(
            CultureInfo.InvariantCulture,
            $"Wrote {BearcatPaths.WindowsServiceConfigPath}"
        );

        int result;
        if (OperatingSystem.IsWindows())
        {
            result = await RegisterServiceAsync(urls, cancellationToken);
        }
        else
        {
            AnsiConsole.MarkupLine(
                "[yellow]Service registration and access hardening are skipped on non-Windows platforms.[/]"
            );
            result = 0;
        }

        if (result == 0)
        {
            if (OperatingSystem.IsWindows())
            {
                foreach (var workingDirectory in workingDirectories.Where(IsUncPath))
                {
                    PrintNetworkPathNotice(workingDirectory);
                }
            }

            PrintKeyBackupNotice(database);
        }

        return result;
    }

    private static List<DatabaseProvider> GetDatabaseProviderChoicesWithPreselectedFirst()
    {
        var preselectedProvider = File.Exists(BearcatPaths.WindowsServiceConfigPath)
            ? ServiceConfigFile
                .Load(BearcatPaths.WindowsServiceConfigPath)
                .Database.EffectiveProvider
            : DatabaseProvider.Sqlite;

        return preselectedProvider == DatabaseProvider.Sqlite
            ? [DatabaseProvider.Sqlite, DatabaseProvider.Postgres]
            : [DatabaseProvider.Postgres, DatabaseProvider.Sqlite];
    }

    private static string GetDisplayName(DatabaseProvider databaseProvider) =>
        databaseProvider == DatabaseProvider.Sqlite ? "SQLite" : "PostgreSQL";

    private static async Task<ServiceConfigFile.DatabaseSection> PromptSqliteDatabaseAsync(
        CancellationToken cancellationToken
    )
    {
        var defaultSqliteFilePath = Path.Combine(
            Path.GetDirectoryName(BearcatPaths.WindowsServiceConfigPath)!,
            "bearcat.db"
        );
        var sqliteFilePath = await AnsiConsole.PromptAsync(
            new TextPrompt<string>("SQLite database file:")
                .DefaultValue(defaultSqliteFilePath)
                .Validate(ValidateSqliteFilePath),
            cancellationToken
        );

        return new ServiceConfigFile.DatabaseSection
        {
            Provider = DatabaseProvider.Sqlite,
            SqliteFilePath = sqliteFilePath.Trim(),
        };
    }

    private static async Task<ServiceConfigFile.DatabaseSection?> PromptPostgresDatabaseAsync(
        CancellationToken cancellationToken
    )
    {
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Database host:").DefaultValue("localhost"),
                cancellationToken
            ),
            Port = await AnsiConsole.PromptAsync(
                new TextPrompt<int>("Database port:").DefaultValue(5432),
                cancellationToken
            ),
            Database = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Database name:").DefaultValue("bearcat"),
                cancellationToken
            ),
            Username = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Database username:").DefaultValue("bearcat"),
                cancellationToken
            ),
            Password = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("Database password:").Secret(),
                cancellationToken
            ),
        }.ConnectionString;

        AnsiConsole.WriteLine("Testing database connection...");
        var (success, error) = await ConfigValidation.TestPostgresConnectionAsync(
            connectionString,
            cancellationToken
        );
        if (!success)
        {
            AnsiConsole.MarkupLineInterpolated(
                CultureInfo.InvariantCulture,
                $"[red]Could not connect to the database:[/] {error}"
            );
            return null;
        }

        AnsiConsole.MarkupLine("[green]Database connection OK.[/]");

        return new ServiceConfigFile.DatabaseSection
        {
            Provider = DatabaseProvider.Postgres,
            ConnectionString = connectionString,
        };
    }

    private static async Task<int> RegisterServiceAsync(
        string urls,
        CancellationToken cancellationToken
    )
    {
        var configDirectory = Path.GetDirectoryName(BearcatPaths.WindowsServiceConfigPath)!;
        WindowsServiceController.RestrictAccess(configDirectory);
        AnsiConsole.MarkupLine("Restricted access to the configuration directory.");

        if (WindowsServiceController.Exists())
        {
            AnsiConsole.MarkupLine(
                "Service already exists; keeping its registration and restarting it with the new configuration."
            );
            WindowsServiceController.Stop();
        }
        else
        {
            var hostExecutable = await ResolveHostExecutableAsync(cancellationToken);
            if (WindowsServiceController.Create(hostExecutable) != 0)
            {
                AnsiConsole.MarkupLine("[red]Failed to create the Windows service.[/]");
                return 1;
            }

            WindowsServiceController.ConfigureRecovery();
            AnsiConsole.MarkupLineInterpolated(
                CultureInfo.InvariantCulture,
                $"Registered Windows service '{WindowsServiceController.ServiceName}'."
            );
        }

        if (WindowsServiceController.Start() != 0)
        {
            AnsiConsole.MarkupLine("[red]Failed to start the service.[/]");
            return 1;
        }

        AnsiConsole.WriteLine("Waiting for the service to become healthy...");
        var healthy = await WindowsServiceController.WaitForHealthAsync(
            urls,
            TimeSpan.FromSeconds(45),
            cancellationToken
        );
        AnsiConsole.MarkupLine(
            healthy
                ? "[green]Service is up and healthy.[/]"
                : "[yellow]Service did not report healthy within 45s; check the Windows Event Log.[/]"
        );

        return 0;
    }

    private static ValidationResult ValidateWorkingDirectory(string path)
    {
        if (OperatingSystem.IsWindows() && IsMappedNetworkDrive(path))
        {
            return ValidationResult.Error(
                "Mapped drives are not visible to the service. Use a UNC path (\\\\server\\share) instead."
            );
        }

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateSqliteFilePath(string path)
    {
        var trimmedPath = path.Trim();
        if (
            IsUncPath(trimmedPath)
            || (OperatingSystem.IsWindows() && IsMappedNetworkDrive(trimmedPath))
        )
        {
            return ValidationResult.Error(
                "The SQLite database file must be on a local drive, not on a network share."
            );
        }

        var (success, error) = ConfigValidation.TestSqliteFileLocation(trimmedPath);
        return success ? ValidationResult.Success() : ValidationResult.Error(error!);
    }

    private static bool IsMappedNetworkDrive(string path)
    {
        var root = Path.GetPathRoot(path);
        if (root is null || root.Length < 2 || root[1] != ':')
        {
            return false;
        }

        return new DriveInfo(root).DriveType == DriveType.Network;
    }

    private static bool IsUncPath(string path) => path.StartsWith(@"\\", StringComparison.Ordinal);

    private static void PrintNetworkPathNotice(string path)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated(
            CultureInfo.InvariantCulture,
            $"[yellow]The working directory {path} is a network path.[/]"
        );
        AnsiConsole.MarkupLine(
            "The service runs as LocalSystem, which cannot reach a protected share. "
                + "Open services.msc -> Bearcat -> 'Log On', set an account that can access the share, then restart the service."
        );
    }

    private static ValidationResult ValidateExecutable(string path) =>
        ConfigValidation.ExecutableIsValid(path)
            ? ValidationResult.Success()
            : ValidationResult.Error($"File not found: {path}");

    private static async Task<string> ResolveHostExecutableAsync(
        CancellationToken cancellationToken
    )
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, HostExecutableName);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        return await AnsiConsole.PromptAsync(
            new TextPrompt<string>($"Path to {HostExecutableName}:").Validate(path =>
                File.Exists(path)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("File not found")
            ),
            cancellationToken
        );
    }

    private static void PrintKeyBackupNotice(ServiceConfigFile.DatabaseSection database)
    {
        var keyPath = Path.Combine(
            Path.GetDirectoryName(BearcatPaths.WindowsServiceConfigPath)!,
            "bearcat.key"
        );
        var databaseDescription =
            database.EffectiveProvider == DatabaseProvider.Sqlite
                ? database.SqliteFilePath
                : "your database";
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated(
            CultureInfo.InvariantCulture,
            $"[yellow]Back up {keyPath} together with {databaseDescription}.[/]"
        );
        AnsiConsole.MarkupLine(
            "Losing this key makes encrypted secrets in the database unrecoverable."
        );
    }
}
