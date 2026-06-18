using Npgsql;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Bearcat.Cli.Commands;

public sealed class SetDbPasswordCommand : AsyncCommand
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken
    )
    {
        if (!ElevationGuard.EnsureElevated())
        {
            return 1;
        }

        if (!File.Exists(BearcatPaths.WindowsServiceConfigPath))
        {
            AnsiConsole.MarkupLineInterpolated(
                $"No configuration found at {BearcatPaths.WindowsServiceConfigPath}. Run 'setup' first."
            );
            return 1;
        }

        var config = ServiceConfigFile.Load(BearcatPaths.WindowsServiceConfigPath);
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(
            config.Database.ConnectionString
        )
        {
            Password = await AnsiConsole.PromptAsync(
                new TextPrompt<string>("New database password:").Secret(),
                cancellationToken
            ),
        };

        AnsiConsole.WriteLine("Testing database connection...");
        var (success, error) = await ConfigValidation.TestDatabaseConnectionAsync(
            connectionStringBuilder.ConnectionString,
            cancellationToken
        );
        if (!success)
        {
            AnsiConsole.MarkupLineInterpolated(
                $"[red]Could not connect with the new password:[/] {error}"
            );
            AnsiConsole.MarkupLine("Configuration left unchanged.");
            return 1;
        }

        config.Database.ConnectionString = connectionStringBuilder.ConnectionString;
        config.Save(BearcatPaths.WindowsServiceConfigPath);
        AnsiConsole.MarkupLine("[green]Updated the connection string.[/]");

        if (!OperatingSystem.IsWindows())
        {
            AnsiConsole.MarkupLine("Restart the service manually for the change to take effect.");
            return 0;
        }

        AnsiConsole.WriteLine("Restarting the service...");
        WindowsServiceController.Stop();
        if (WindowsServiceController.Start() != 0)
        {
            AnsiConsole.MarkupLine("[red]Failed to start the service; start it manually.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[green]Service restarted.[/]");
        return 0;
    }
}
