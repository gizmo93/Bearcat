using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Bearcat.Launcher;

public static class BearcatStartup
{
    private static Process? _hostProcess;

    public static async Task StartupBearcatAsync(string[] args, CancellationToken cancellationToken)
    {
        var assemblyPath = Assembly.GetExecutingAssembly().Location;

        var urls = "https://localhost:7208;http://localhost:5097";

        // Starte den Host als separaten Prozess
        _hostProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{assemblyPath}\" --urls {urls} {string.Join(" ", args)}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(assemblyPath)
            }
        };

        // Log Output für Debugging
        _hostProcess.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.WriteLine($"[Host] {e.Data}");
            }
        };

        _hostProcess.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.Error.WriteLine($"[Host Error] {e.Data}");
            }
        };

        _hostProcess.Start();
        _hostProcess.BeginOutputReadLine();
        _hostProcess.BeginErrorReadLine();

        // Warte auf Cancellation
        await Task.Run(() =>
        {
            cancellationToken.WaitHandle.WaitOne();
            StopHost();
        }, cancellationToken);
    }

    public static void StopHost()
    {
        if (_hostProcess != null && !_hostProcess.HasExited)
        {
            try
            {
                _hostProcess.Kill(entireProcessTree: true);
                _hostProcess.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fehler beim Beenden des Host-Prozesses: {ex.Message}");
            }
        }
    }
}
