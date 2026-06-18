using Spectre.Console;
using Spectre.Console.Cli;

namespace Bearcat.Cli.Commands;

public sealed class UninstallCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            AnsiConsole.MarkupLine("The Windows service can only be removed on Windows.");
            return 1;
        }

        if (!ElevationGuard.EnsureElevated())
        {
            return 1;
        }

        WindowsServiceController.Stop();
        if (WindowsServiceController.Delete() != 0)
        {
            AnsiConsole.MarkupLine("[red]Failed to remove the Windows service.[/]");
            return 1;
        }

        AnsiConsole.MarkupLineInterpolated(
            $"Removed Windows service '{WindowsServiceController.ServiceName}'."
        );

        if (
            File.Exists(BearcatPaths.WindowsServiceConfigPath)
            && AnsiConsole.Confirm(
                $"Delete {BearcatPaths.WindowsServiceConfigPath}?",
                defaultValue: false
            )
        )
        {
            File.Delete(BearcatPaths.WindowsServiceConfigPath);
            AnsiConsole.MarkupLine("Deleted the configuration file.");
        }

        return 0;
    }
}
