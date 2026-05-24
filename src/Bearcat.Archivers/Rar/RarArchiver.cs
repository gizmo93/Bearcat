using System.Diagnostics;
using Bearcat.Abstractions.Archiver;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bearcat.Archivers.Rar;

public class RarArchiver(ILogger<RarArchiver> logger, IConfiguration configuration) : IArchiver
{
    public string Name => "RAR";

    public string FileExtension => ".rar";

    public async Task<ArchiveResult> ArchiveAsync(
        string sourceFolderPath,
        string destinationPath,
        string archiveNamePrefix,
        int targetFileSizeMb,
        string? password,
        ArchiveOptions options,
        CancellationToken cancellationToken
    )
    {
        var commandLineArgs = CreateCommandLineArguments(
            sourceFolderPath: sourceFolderPath,
            destinationPath: destinationPath,
            archiveNamePrefix: archiveNamePrefix,
            targetFileSizeMb: targetFileSizeMb,
            password: password,
            options: options
        );

        var processStartInfo = new ProcessStartInfo
        {
            FileName = GetExecutablePath(),
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
                logger.LogInformation("RAR Output: {OutputData}", e.Data);
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
        var files = directoryInfo.GetFiles($"{archiveNamePrefix}*.rar");

        return files.Select(f => f.FullName).ToList();
    }

    private static string CreateCommandLineArguments(
        string sourceFolderPath,
        string destinationPath,
        string archiveNamePrefix,
        int targetFileSizeMb,
        string? password,
        ArchiveOptions options
    )
    {
        var archiveFullPath = Path.Combine(destinationPath, archiveNamePrefix + ".rar");
        var passwordPart = !string.IsNullOrWhiteSpace(password) ? $"-p{password}" : string.Empty;
        var compressionPart = options.UseCompression ? "-m1" : "-m0";
        var solidPart = options.UseSolidArchive ? "-s" : "-s-";

        return $"a -ep1 {compressionPart} {solidPart} -v{targetFileSizeMb}m {passwordPart} \"{archiveFullPath}\" \"{sourceFolderPath}\"/*";
    }

    private string GetExecutablePath()
    {
        var configuredPath = configuration["Archivers:RarPath"];
        return string.IsNullOrWhiteSpace(configuredPath) ? "rar" : configuredPath;
    }
}
