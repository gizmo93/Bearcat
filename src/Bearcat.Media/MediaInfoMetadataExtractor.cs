using System.Diagnostics;
using Bearcat.Abstractions.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bearcat.Media;

public class MediaInfoMetadataExtractor(
    ILogger<MediaInfoMetadataExtractor> logger,
    IConfiguration configuration
) : IMediaMetadataExtractor
{
    public async Task<MediaProbeResult?> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken
    )
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await RunAsync(filePath, withJsonOutput: true, cancellationToken);

        if (json is null)
        {
            return null;
        }

        var text = await RunAsync(filePath, withJsonOutput: false, cancellationToken);
        if (text is null)
        {
            return null;
        }

        return new MediaProbeResult(Json: json, Text: text);
    }

    private async Task<string?> RunAsync(
        string filePath,
        bool withJsonOutput,
        CancellationToken cancellationToken
    )
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = GetExecutablePath(),
            WorkingDirectory = Path.GetDirectoryName(filePath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (withJsonOutput)
        {
            processStartInfo.ArgumentList.Add("--Output=JSON");
        }

        processStartInfo.ArgumentList.Add(Path.GetFileName(filePath));

        try
        {
            using var process = new Process();
            process.StartInfo = processStartInfo;
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                logger.LogWarning(
                    "mediainfo failed for {FilePath} with exit code {ExitCode}: {Error}",
                    filePath,
                    process.ExitCode,
                    error
                );
                return null;
            }

            return output;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to extract media metadata for {FilePath}", filePath);
            return null;
        }
    }

    private string GetExecutablePath()
    {
        return MediaInfoBinary.Resolve(configuration["Media:MediaInfoPath"]);
    }
}
