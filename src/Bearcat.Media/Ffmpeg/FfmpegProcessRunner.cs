using System.Diagnostics;

namespace Bearcat.Media.Ffmpeg;

public static class FfmpegProcessRunner
{
    public static async Task<string> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken
    )
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        using var process = new Process();
        process.StartInfo = processStartInfo;
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"ffmpeg exited with code {process.ExitCode}: {error.Trim()}"
            );
        }

        return output;
    }
}
