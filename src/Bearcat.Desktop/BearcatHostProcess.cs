using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Bearcat.Desktop;

public sealed class BearcatHostProcess : IDisposable
{
    private readonly HttpClient httpClient = new();
    private Process? process;

    public event Action<string>? LogReceived;

    public event Action<int?>? Exited;

    public bool IsRunning => process is { HasExited: false };

    public async Task StartAsync(DesktopSettings settings)
    {
        if (IsRunning)
        {
            return;
        }

        var startInfo = CreateStartInfo(settings);
        process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => WriteLog(e.Data);
        process.ErrorDataReceived += (_, e) => WriteLog(e.Data);
        process.Exited += (_, _) => Exited?.Invoke(process?.ExitCode);

        if (!process.Start())
        {
            throw new InvalidOperationException("Bearcat.Host could not be started.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await WaitForHealthAsync(settings.WebUrl, timeout.Token);
    }

    public async Task StopAsync()
    {
        if (!IsRunning || process is null)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
        finally
        {
            process.Dispose();
            process = null;
        }
    }

    public void Dispose()
    {
        if (process is { HasExited: false })
        {
            process.Kill(entireProcessTree: true);
        }

        process?.Dispose();
        httpClient.Dispose();
    }

    private static ProcessStartInfo CreateStartInfo(DesktopSettings settings)
    {
        var resolvedHost = BearcatHostResolver.Resolve(settings.BearcatHostPath);
        var startInfo = resolvedHost.CreateStartInfo();
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        var environment = resolvedHost.UseDevelopmentEnvironment ? "Development" : "Production";

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = environment;
        startInfo.Environment["DOTNET_ENVIRONMENT"] = environment;
        startInfo.Environment["DOTNET_gcServer"] = "0"; // Workstation mode to save RAM. Default is Server Mode.
        startInfo.Environment["ASPNETCORE_URLS"] = settings.WebUrl;
        startInfo.Environment["Bearcat__DesktopMode"] = "true";
        startInfo.Environment["Database__ConnectionString"] = settings.CreateConnectionString();
        startInfo.Environment["ReleaseDataDirectory"] = settings.ReleaseDataDirectory;
        startInfo.Environment["Archivers__RarPath"] = settings.RarPath;
        startInfo.Environment["Archivers__SevenZipPath"] = settings.SevenZipPath;

        return startInfo;
    }

    private async Task WaitForHealthAsync(string webUrl, CancellationToken cancellationToken)
    {
        var healthUrl = $"{webUrl.TrimEnd('/')}/health";

        while (!cancellationToken.IsCancellationRequested)
        {
            if (process is { HasExited: true })
            {
                throw new InvalidOperationException(
                    $"Bearcat.Host exited before it became healthy. Exit code: {process.ExitCode}."
                );
            }

            try
            {
                using var response = await httpClient.GetAsync(healthUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }

            await Task.Delay(500, cancellationToken);
        }

        throw new InvalidOperationException("Bearcat.Host did not become healthy in time.");
    }

    private void WriteLog(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            LogReceived?.Invoke(message);
        }
    }
}
