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

        var releaseDataDirectory = await AnsiConsole.PromptAsync(
            new TextPrompt<string>("Release data directory:").Validate(ValidateReleaseDirectory),
            cancellationToken
        );
        if (!Directory.Exists(releaseDataDirectory))
        {
            if (
                !await AnsiConsole.ConfirmAsync(
                    $"{releaseDataDirectory} does not exist. Create it?",
                    cancellationToken: cancellationToken
                )
            )
            {
                AnsiConsole.MarkupLine("[yellow]Aborted.[/]");
                return 1;
            }

            Directory.CreateDirectory(releaseDataDirectory);
        }

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
        var (success, error) = await ConfigValidation.TestDatabaseConnectionAsync(
            connectionString,
            cancellationToken
        );
        if (!success)
        {
            AnsiConsole.MarkupLineInterpolated(
                $"[red]Could not connect to the database:[/] {error}"
            );
            return 1;
        }

        AnsiConsole.MarkupLine("[green]Database connection OK.[/]");

        var webPort = await AnsiConsole.PromptAsync(
            new TextPrompt<int>("Web port:").DefaultValue(17208),
            cancellationToken
        );
        var urls = $"http://127.0.0.1:{webPort}";

        new ServiceConfigFile
        {
            Database = { ConnectionString = connectionString },
            Archivers = { RarPath = rarPath, SevenZipPath = sevenZipPath },
            ReleaseDataDirectory = releaseDataDirectory,
            Urls = urls,
        }.Save(BearcatPaths.WindowsServiceConfigPath);
        AnsiConsole.MarkupLineInterpolated($"Wrote {BearcatPaths.WindowsServiceConfigPath}");

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
            if (OperatingSystem.IsWindows() && IsUncPath(releaseDataDirectory))
            {
                PrintNetworkPathNotice(releaseDataDirectory);
            }

            PrintKeyBackupNotice();
        }

        return result;
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

    private static ValidationResult ValidateReleaseDirectory(string path)
    {
        if (OperatingSystem.IsWindows() && IsMappedNetworkDrive(path))
        {
            return ValidationResult.Error(
                "Mapped drives are not visible to the service. Use a UNC path (\\\\server\\share) instead."
            );
        }

        return ValidationResult.Success();
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
            $"[yellow]The release directory {path} is a network path.[/]"
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

    private static void PrintKeyBackupNotice()
    {
        var keyPath = Path.Combine(
            Path.GetDirectoryName(BearcatPaths.WindowsServiceConfigPath)!,
            "bearcat.key"
        );
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated(
            $"[yellow]Back up {keyPath} together with your database.[/]"
        );
        AnsiConsole.MarkupLine(
            "Losing this key makes encrypted secrets in the database unrecoverable."
        );
    }
}
