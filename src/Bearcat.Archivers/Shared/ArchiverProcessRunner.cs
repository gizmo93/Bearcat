using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Bearcat.Archivers.Shared;

public class ArchiverProcessRunner(ILogger<ArchiverProcessRunner> logger)
{
    public async Task<ArchiverProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string archiverName,
        CancellationToken cancellationToken
    )
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardInput = true,
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

        var outputLines = new List<string>();
        var errorLines = new List<string>();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                logger.LogInformation("{ArchiverName} output: {OutputLine}", archiverName, e.Data);
                outputLines.Add(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorLines.Add(e.Data);
            }
        };

        process.Start();
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new ArchiverProcessResult(
            ExitCode: process.ExitCode,
            OutputLines: outputLines,
            ErrorLines: errorLines
        );
    }
}
