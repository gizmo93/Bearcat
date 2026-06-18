using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace Bearcat.Cli;

public static class WindowsServiceController
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public const string ServiceName = "Bearcat";

    private const string DisplayName = "Bearcat";

    [SupportedOSPlatform("windows")]
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static int Create(string executablePath)
    {
        return RunSc(
            $"create {ServiceName} binPath= \"\\\"{executablePath}\\\"\" start= auto DisplayName= \"{DisplayName}\""
        );
    }

    public static int ConfigureRecovery()
    {
        return RunSc($"failure {ServiceName} reset= 86400 actions= restart/5000");
    }

    public static int Start()
    {
        return RunSc($"start {ServiceName}");
    }

    public static int Stop()
    {
        return RunSc($"stop {ServiceName}");
    }

    public static int Delete()
    {
        return RunSc($"delete {ServiceName}");
    }

    public static bool Exists()
    {
        var startInfo = new ProcessStartInfo("sc.exe", $"query {ServiceName}")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        process.WaitForExit();
        return process.ExitCode == 0;
    }

    public static int RestrictAccess(string directoryPath)
    {
        // Limit access to the Bearcat folder to the Service Account & Admins
        return Run(
            "icacls",
            $"\"{directoryPath}\" /inheritance:r /grant:r \"*S-1-5-18:(OI)(CI)F\" \"*S-1-5-32-544:(OI)(CI)F\""
        );
    }

    public static async Task<bool> WaitForHealthAsync(
        string url,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await HttpClient.GetAsync($"{url}/health", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // not reachable yet; keep polling until the timeout
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        return false;
    }

    private static int RunSc(string arguments)
    {
        return Run("sc.exe", arguments);
    }

    private static int Run(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments) { UseShellExecute = false };

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            return -1;
        }

        process.WaitForExit();
        return process.ExitCode;
    }
}
