using System.Diagnostics;
using Bearcat.Abstractions.Archiver;
using Microsoft.Extensions.Logging;

namespace Bearcat.Archivers._7Zip;

public class SevenZipArchiver(ILogger<SevenZipArchiver> logger) : IArchiver
{
    public string Name => "7Zip";

    public string FileExtension => ".7z";

    public async Task<ArchiveResult> ArchiveAsync(
        string sourceFolderPath,
        string destinationPath,
        string archiveNamePrefix,
        int targetFileSizeMb,
        string? password,
        CancellationToken cancellationToken
    )
    {
        var commandLineArgs = CreateCommandLineArguments(
            sourceFolderPath: sourceFolderPath,
            destinationPath: destinationPath,
            archiveNamePrefix: archiveNamePrefix,
            targetFileSizeMb: targetFileSizeMb,
            password: password
        );

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "7z",
            Arguments = commandLineArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = new Process();
        process.StartInfo = processStartInfo;

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var errors = new List<string>();

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                logger.LogInformation("7Z Output: {OutputData}", e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errors.Add(e.Data);
            }
        };

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            return new ArchiveResult(IsSuccess: false, CreatedFileNames: [], ErrorMessages: errors);
        }

        var createdFiles = CollectCreatedFiles(
            destinationPath: destinationPath,
            archiveNamePrefix: archiveNamePrefix
        );

        return new ArchiveResult(
            IsSuccess: true,
            CreatedFileNames: createdFiles,
            ErrorMessages: null
        );
    }

    private static List<string> CollectCreatedFiles(
        string destinationPath,
        string archiveNamePrefix
    )
    {
        var directoryInfo = new DirectoryInfo(destinationPath);
        var files = directoryInfo.GetFiles($"{archiveNamePrefix}.7z*");

        return files.Select(f => f.FullName).ToList();
    }

    private static string CreateCommandLineArguments(
        string sourceFolderPath,
        string destinationPath,
        string archiveNamePrefix,
        int targetFileSizeMb,
        string? password
    )
    {
        var archiveFullPath = Path.Combine(destinationPath, archiveNamePrefix + ".7z");
        var passwordPart = !string.IsNullOrWhiteSpace(password) ? $"-p{password}" : string.Empty;

        return $"a -v{targetFileSizeMb}m -mx=0 {passwordPart} \"{archiveFullPath}\" \"{sourceFolderPath}\"/*";
    }
}
