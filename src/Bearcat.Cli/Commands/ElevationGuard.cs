using Spectre.Console;

namespace Bearcat.Cli.Commands;

internal static class ElevationGuard
{
    public static bool EnsureElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        if (WindowsServiceController.IsElevated())
        {
            return true;
        }

        AnsiConsole.MarkupLine(
            "[red]Administrator rights are required.[/] Re-run from an elevated terminal (right-click -> Run as administrator)."
        );

        return false;
    }
}
